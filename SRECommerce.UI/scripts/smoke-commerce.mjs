import {chromium} from 'playwright-core';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import {join,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {randomBytes} from 'node:crypto';
const root=resolve(fileURLToPath(new URL('../../',import.meta.url))),base=process.env.COMMERCE_SMOKE_UI_URL||'http://localhost:4000';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Explicit local browser smoke URL required.');
const privateConfig=JSON.parse(readFileSync(join(process.env.APPDATA,'Microsoft/UserSecrets/SRECommerce-LegacyDemo-Development/secrets.json'),'utf8'));
const output=join(root,'artifacts/commerce-browser-smoke');mkdirSync(output,{recursive:true});
const browser=await chromium.launch({executablePath:process.env.CHROME_BIN||'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',headless:true});
const page=await browser.newPage({viewport:{width:1365,height:900}});let phase='load workspace';const checks=[];
const failures=[],requests=[];page.on('response',response=>{const url=new URL(response.url());if(url.port==='5181'||url.pathname.startsWith('/commerce/'))requests.push({path:url.pathname,method:response.request().method(),status:response.status()});if(response.status()>=400)failures.push({path:url.pathname,status:response.status()});});
const pageErrors=[];page.on('pageerror',error=>{const code=error.message.match(/NG\d+/)?.[0];pageErrors.push({type:error.name,code:code||'runtime-error'});});
page.on('requestfailed',request=>{if(new URL(request.url()).pathname.startsWith('/commerce/'))failures.push({path:new URL(request.url()).pathname,error:request.failure()?.errorText});});
page.on('console',message=>{if(message.text().startsWith('Commerce workspace failure'))pageErrors.push({type:'workspace-request',diagnostic:message.text().slice(0,200)});});
try{
  await page.goto(base+'/workspace/'+privateConfig.TenantId+'/catalog');
  phase='start PKCE sign-in';await page.getByRole('button',{name:'Secure sign in',exact:true}).click();
  await page.waitForURL(url=>url.port==='5181',{timeout:20000});
  await page.locator('input[name="email"]').fill(privateConfig.OwnerEmail);await page.locator('input[name="password"]').fill(privateConfig.OwnerPassword);
  phase='complete PKCE sign-in';await page.getByRole('button',{name:'Sign in',exact:true}).click();
  await page.waitForURL(base+'/workspace/'+privateConfig.TenantId+'/catalog',{timeout:30000});
  await page.getByRole('button',{name:'New product',exact:true}).waitFor({timeout:20000});checks.push('Real Identity code+PKCE callback and Owner workspace');
  phase='save product draft';const suffix=randomBytes(5).toString('hex'),name='000 Browser smoke '+suffix,slug='browser-smoke-'+suffix;
  await page.getByRole('button',{name:'New product',exact:true}).click();await page.locator('input[name="name"]').fill(name);await page.locator('input[name="slug"]').fill(slug);
  const category=await page.locator('select[name="category"] option').evaluateAll(options=>options.map(o=>o.value).find(Boolean));if(!category)throw new Error('No category available');
  await page.locator('select[name="category"]').selectOption(category);await page.getByLabel('SKU',{exact:true}).fill('UI-'+suffix.toUpperCase());
  await page.getByRole('button',{name:'Save draft',exact:true}).click();await page.getByText('Draft saved. Publish it when it is ready.',{exact:true}).waitFor({timeout:20000});
  const row=page.locator('tbody tr').filter({hasText:name});await row.waitFor({timeout:20000});checks.push('Product draft persisted through gateway to SQL');
  phase='publish product';await row.getByRole('button',{name:'Publish',exact:true}).click();await page.getByText('Product published.',{exact:true}).waitFor({timeout:20000});checks.push('Owner publication committed');
  if(process.argv.includes('--inventory')){
    phase='register published inventory SKU';await page.getByRole('link',{name:'Manage inventory',exact:true}).click();await page.getByRole('heading',{name:'Inventory workspace',exact:true}).waitFor();
    await page.getByLabel('Product search',{exact:true}).fill(suffix);await page.getByRole('button',{name:'Find products',exact:true}).click();
    await page.locator('select[name="product"]').selectOption({label:name});await page.locator('select[name="variant"]').selectOption({label:'UI-'+suffix.toUpperCase()});
    await page.getByRole('button',{name:'Register SKU',exact:true}).click();await page.getByText('Published SKU registered.',{exact:true}).waitFor({timeout:20000});checks.push('Inventory verifies and registers published Catalog SKU');
    phase='receive inventory stock';await page.locator('select[name="operating-warehouse"]').selectOption({label:'Legacy Demo warehouse'});await page.locator('select[name="operating-sku"]').selectOption({label:'UI-'+suffix.toUpperCase()});
    await page.getByLabel('Receipt quantity',{exact:true}).fill('5');await page.getByLabel('Lot code',{exact:true}).fill('BROWSER-'+suffix.toUpperCase());await page.getByLabel('Receipt reason',{exact:true}).fill('Labeled browser verification');
    await page.getByRole('button',{name:'Receive stock',exact:true}).click();await page.getByText('Stock receipt saved.',{exact:true}).waitFor({timeout:20000});checks.push('Opening receipt persisted with lot and SQL ledger');
    phase='reserve and release inventory';await page.getByLabel('Order / hold reference',{exact:true}).fill('BROWSER-'+suffix);await page.getByLabel('Reservation quantity',{exact:true}).fill('3');await page.getByRole('button',{name:'Reserve stock',exact:true}).click();await page.getByText('Stock reserved.',{exact:true}).waitFor({timeout:20000});
    await page.getByRole('button',{name:'Release hold',exact:true}).click();await page.getByText('Reservation released.',{exact:true}).waitFor({timeout:20000});checks.push('Reservation and release update physical availability');
    phase='commit inventory reservation';await page.getByRole('button',{name:'Reserve stock',exact:true}).click();await page.getByText('Stock reserved.',{exact:true}).waitFor({timeout:20000});await page.getByRole('button',{name:'Commit and deduct stock',exact:true}).click();await page.getByText('Reservation committed. Physical stock deducted.',{exact:true}).waitFor({timeout:20000});
    const stockRow=page.locator('tbody tr').filter({hasText:'BROWSER-'+suffix.toUpperCase()});await stockRow.getByRole('cell',{name:'2',exact:true}).first().waitFor({timeout:20000});checks.push('Commit deducts exactly three units and clears the hold');
    await page.screenshot({path:join(output,'inventory-desktop.png'),fullPage:true});await page.setViewportSize({width:390,height:844});await page.evaluate(()=>window.scrollTo(0,0));await page.screenshot({path:join(output,'inventory-mobile.png')});await page.setViewportSize({width:1365,height:900});
  }
  const couponCode='UI10'+suffix.toUpperCase();
  if(process.argv.includes('--pricing')){
    phase='configure pricing currency';await page.getByRole('link',{name:'Manage pricing',exact:true}).click();await page.getByRole('heading',{name:'Pricing workspace',exact:true}).waitFor();await page.getByRole('button',{name:'Save currency settings',exact:true}).click();await page.getByText('Currency settings saved.',{exact:true}).waitFor({timeout:20000});
    phase='configure tax and price list';const taxName='Browser tax '+suffix,listName='Browser retail '+suffix,promotionName='Browser coupon '+suffix;
    await page.locator('input[name="tax-code"]').fill('BROWSER-'+suffix.toUpperCase());await page.locator('input[name="tax-name"]').fill(taxName);await page.locator('input[name="tax-rate"]').fill('18');await page.getByRole('button',{name:'Save tax policy',exact:true}).click();await page.getByText('Tax policy saved.',{exact:true}).waitFor({timeout:20000});
    await page.locator('input[name="list-code"]').fill('BROWSER-'+suffix.toUpperCase());await page.locator('input[name="list-name"]').fill(listName);await page.getByRole('button',{name:'Save price list',exact:true}).click();await page.getByText('Price list saved.',{exact:true}).waitFor({timeout:20000});checks.push('Owner configures currency, tax and scheduled retail price list');
    phase='create SKU price';await page.locator('input[name="product-search"]').fill(suffix);await page.getByRole('button',{name:'Find published products',exact:true}).click();await page.locator('select[name="price-product"]').selectOption({label:name});await page.locator('select[name="price-variant"]').selectOption({label:'UI-'+suffix.toUpperCase()});
    await page.locator('select[name="price-list"]').selectOption({label:listName});await page.locator('select[name="price-tax"]').selectOption({label:taxName});await page.locator('input[name="mrp"]').fill('120');await page.locator('input[name="selling-price"]').fill('100');await page.locator('input[name="cost-price"]').fill('60');await page.getByRole('button',{name:'Save SKU price',exact:true}).click();await page.getByText('SKU price saved.',{exact:true}).waitFor({timeout:20000});checks.push('Verified SKU price and private cost persist in Pricing SQL');
    phase='create promotion and coupon';await page.locator('input[name="promotion-code"]').fill('BROWSER-'+suffix.toUpperCase());await page.locator('input[name="promotion-name"]').fill(promotionName);await page.locator('input[name="promotion-automatic"]').uncheck();await page.locator('select[name="promotion-targets"]').selectOption({label:'UI-'+suffix.toUpperCase()});await page.getByRole('button',{name:'Save promotion',exact:true}).click();await page.getByText('Promotion saved.',{exact:true}).waitFor({timeout:20000});
    await page.locator('input[name="coupon-code"]').fill(couponCode);await page.locator('select[name="coupon-promotion"]').selectOption({label:promotionName});await page.locator('input[name="coupon-customer"]').fill('0');await page.getByRole('button',{name:'Save coupon',exact:true}).click();await page.getByText('Coupon saved.',{exact:true}).waitFor({timeout:20000});checks.push('Targeted promotion and bounded guest coupon configured');
    await page.screenshot({path:join(output,'pricing-desktop.png'),fullPage:true});await page.setViewportSize({width:390,height:844});await page.evaluate(()=>window.scrollTo(0,0));await page.screenshot({path:join(output,'pricing-mobile.png')});await page.setViewportSize({width:1365,height:900});
  }
  phase='public catalog';await page.goto(base+'/stores/legacy-demo');await page.getByRole('link',{name,exact:true}).first().waitFor({timeout:20000});
  await page.screenshot({path:join(output,'catalog-desktop.png'),fullPage:true});checks.push('Published product visible in live public catalog');
  phase='public product';await page.getByRole('link',{name,exact:true}).first().click();await page.getByRole('heading',{name,exact:true}).waitFor({timeout:20000});await page.getByRole('button',{name:new RegExp('UI-'+suffix.toUpperCase())}).waitFor();checks.push('Product details and sellable SKU rendered');
  if(process.argv.includes('--inventory')){await page.getByText(/2 units available\./).waitFor({timeout:20000});checks.push('Public product reads the real remaining Inventory quantity');}
  if(process.argv.includes('--pricing')){
    phase='public authoritative price';await page.getByTestId('quote-total').filter({hasText:'118.00'}).waitFor({timeout:20000});await page.locator('input[name="quote-coupon"]').fill(couponCode);await page.getByRole('button',{name:'Apply / refresh price',exact:true}).click();await page.getByTestId('quote-total').filter({hasText:'106.20'}).waitFor({timeout:20000});checks.push('Public price includes server tax and validated coupon savings');
    phase='public quantity repricing';await page.locator('input[name="quote-quantity"]').fill('2');await page.getByTestId('quote-total').filter({hasText:'212.40'}).waitFor({timeout:20000});checks.push('Quantity changes trigger authoritative repricing');
  }
  await page.setViewportSize({width:390,height:844});await page.screenshot({path:join(output,'product-mobile.png'),fullPage:true});
  if(await page.evaluate(()=>Object.keys(localStorage).some(k=>k.startsWith('oidc.'))))throw new Error('Unexpected persistent OIDC user storage');checks.push('No OIDC user tokens persisted to localStorage');
  writeFileSync(join(output,'result.json'),JSON.stringify({passed:checks.length,checks,productSlug:slug,source:'real local SQL/Identity/Tenant/Catalog'+(process.argv.includes('--inventory')?'/Inventory':'')+(process.argv.includes('--pricing')?'/Pricing':'')+' via gateway',credentialsPrinted:false},null,2));
  console.log(JSON.stringify({passed:checks.length,checks,credentialsPrinted:false}));
}catch(error){
  const path=new URL(page.url()).pathname;
  await page.screenshot({path:join(output,'failure.png'),fullPage:true});
  const fields=await page.locator('input').evaluateAll(inputs=>inputs.filter(i=>i.type!=='hidden').map(i=>({name:i.name,type:i.type,valid:i.checkValidity(),length:i.value.length})));
  writeFileSync(join(output,'failure.json'),JSON.stringify({phase,path,failures,requests,fields,pageErrors,errorType:error?.name||'Error',credentialsPrinted:false},null,2));
  console.error('Commerce browser smoke failed during '+phase+'; private credentials/tokens were not printed.');process.exitCode=1;
}finally{await browser.close();}
