import {chromium} from 'playwright-core';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {join} from 'node:path';
const root=fileURLToPath(new URL('../../',import.meta.url));
const result=JSON.parse(readFileSync(join(root,'artifacts/commerce-browser-smoke/result.json'),'utf8'));
const browser=await chromium.launch({executablePath:process.env.CHROME_BIN||'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',headless:true});
try{const page=await browser.newPage({viewport:{width:390,height:844}});await page.goto('http://localhost:4000/stores/legacy-demo/products/'+result.productSlug);await page.getByTestId('quote-total').waitFor({timeout:20000});if(await page.locator('app-shop-chat').count())throw new Error('Legacy overlay must not cover commerce controls');await page.screenshot({path:join(root,'artifacts/commerce-browser-smoke/product-mobile-final.png'),fullPage:true});console.log('Public mobile quote renders and commerce controls have no legacy chat overlay.');}finally{await browser.close();}
