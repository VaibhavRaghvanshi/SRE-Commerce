import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';

// Read-only recovery audit: no migration, database setting or credential writes.
const tenant = '25bdef41-42e9-4d89-8d2b-0b0b295f61d0';
const services = ['Identity','Tenant','Theme','Storefront','Catalog','Inventory','Pricing','Cart','Order','Payment','Shipping','Notification','Manufacturing','Discovery'];
const expectedHash = 'fc6d183d231508e14f9505523de86b94b6891af18e55692d144f27932aaefe01';
const mapping = JSON.parse(readFileSync('artifacts/legacy-demo-catalog-mapping.json', 'utf8'));
const originalIds = Object.values(mapping.products || {});
if (mapping.tenantId !== tenant || originalIds.length !== 1000 || new Set(originalIds).size !== 1000 || originalIds.some(id => !/^[a-f0-9-]{36}$/i.test(id))) throw new Error('OriginalMappingMismatch');
function query(database, statement) {
  try {
    const text = execFileSync('sqlcmd', ['-S','tcp:localhost,1433','-E','-C','-b','-I','-l','8','-t','20','-y','0','-w','65535','-d',database,'-Q','SET NOCOUNT ON; '+statement], { encoding: 'utf8', timeout: 35000, windowsHide: true, stdio: ['ignore','pipe','pipe'] });
    return JSON.parse(text.split(/\r?\n/).filter(line => line.trim() && !/^JSON_|^-+$/.test(line.trim())).join(''));
  } catch { throw new Error('ReadOnlySqlProbeFailed'); }
}
function assert(value, code) { if (!value) throw new Error(code); }
const checks = [];
try {
  const instance = query('master', "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName')) AS instanceName,CONVERT(int,SERVERPROPERTY('IsSingleUser')) AS singleUser,(SELECT CONVERT(bigint,value_in_use) FROM sys.configurations WHERE name='max server memory (MB)') AS maxMemoryMB FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;");
  assert(instance.instanceName === 'MSSQL22' && instance.singleUser === 0 && instance.maxMemoryMB === 2147483647, 'OriginalSqlConfigurationNotRestored'); checks.push('MSSQL22 accepts normal connections with its original memory setting');
  const databases = query('master', 'SELECT name,state_desc AS state FROM sys.databases FOR JSON PATH;');
  assert(services.every(service => databases.some(db => db.name === 'SRECommerce_'+service+'DB' && db.state === 'ONLINE')), 'MainDatabaseNotOnline'); checks.push('all fourteen main service databases are online');
  const products = query('SRECommerce_CatalogDB', `SELECT CONVERT(varchar(36),Id) AS id,Status AS status,PublishedRevision AS publishedRevision FROM dbo.Products WHERE TenantId='${tenant}' FOR JSON PATH;`);
  const existing = new Map(products.map(product => [product.id.toLowerCase(), product]));
  assert(originalIds.every(id => existing.has(id.toLowerCase()) && existing.get(id.toLowerCase()).status !== 2 && existing.get(id.toLowerCase()).publishedRevision > 0), 'MappedOriginalProductMissing'); checks.push('all 1000 original mapped products remain present and published');
  const orders = query('SRECommerce_OrderDB', `SELECT COUNT(*) AS count,CONVERT(decimal(18,2),SUM(Total)) AS total,CONVERT(decimal(18,2),SUM(ShippingTotal)) AS shipping FROM dbo.Orders WHERE TenantId='${tenant}' AND Historical=1 FOR JSON PATH,WITHOUT_ARRAY_WRAPPER;`);
  assert(orders.count === 3 && orders.total === 474789 && orders.shipping === 297, 'HistoricalOrderSnapshotMismatch'); checks.push('three historical order totals and delivery charges reconcile');
  const sourceHash = createHash('sha256').update(readFileSync('SRECommerce.Legacy.API/SRECommerce.Api/App_Data/srecommerce.db')).digest('hex');
  assert(sourceHash === expectedHash, 'OriginalSqliteHashChanged'); checks.push('original SQLite SHA256 is unchanged');
} catch (error) { process.exitCode = 1; console.error(JSON.stringify({ failureType: error.message, privateDetailsOmitted: true })); }
mkdirSync('artifacts/review-website', { recursive: true });
writeFileSync('artifacts/review-website/data-audit.json', JSON.stringify({ sampledAt: new Date().toISOString(), checks, success: !process.exitCode }, null, 2));
console.log(JSON.stringify({ checks: checks.length, success: !process.exitCode }));
