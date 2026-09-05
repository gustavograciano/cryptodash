import puppeteer from 'puppeteer';
import { mkdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const OUT = resolve(__dirname, 'screenshots');
const URL_BASE = process.env.URL_BASE ?? 'http://localhost:5174';
const EMAIL = process.env.SEED_EMAIL ?? 'gustavo@cryptodash.local';
const PASSWORD = process.env.SEED_PASSWORD;
if (!PASSWORD) {
  console.error('Defina SEED_PASSWORD antes de rodar a captura.');
  process.exit(1);
}

await mkdir(OUT, { recursive: true });

const browser = await puppeteer.launch({
  headless: 'new',
  defaultViewport: { width: 1440, height: 900, deviceScaleFactor: 2 },
});
const page = await browser.newPage();

const shot = async (name) => {
  const path = resolve(OUT, `${name}.png`);
  await page.screenshot({ path, fullPage: false });
  console.log('saved', name);
};

const fullShot = async (name) => {
  const path = resolve(OUT, `${name}.png`);
  await page.screenshot({ path, fullPage: true });
  console.log('saved', name, '(full)');
};

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

try {
  // 1. Login
  await page.goto(`${URL_BASE}/login`, { waitUntil: 'networkidle0' });
  await sleep(500);
  await shot('01-login');

  await page.type('input[type="email"]', '', { delay: 0 });
  // ensure we don't double-fill (email is prefilled)
  await page.evaluate(() => {
    document.querySelectorAll('input').forEach((i) => { i.value = ''; });
  });
  await page.type('input[type="email"]', EMAIL);
  await page.type('input[type="password"]', PASSWORD);
  await shot('02-login-filled');

  await Promise.all([
    page.waitForNavigation({ waitUntil: 'networkidle0' }),
    page.click('button[type="submit"]'),
  ]);
  await sleep(1500);

  // 2. Dashboard top (full)
  await fullShot('03-dashboard-completo');

  // 3. Dashboard topo
  await page.evaluate(() => window.scrollTo(0, 0));
  await sleep(300);
  await shot('04-dashboard-topo');

  // 4. NextAportCard + Stats
  await sleep(300);
  await shot('05-next-aporte');

  // 5. Holdings + Donut
  await page.evaluate(() => window.scrollBy(0, 200));
  await sleep(300);
  await shot('06-holdings-donut');

  // 6. Desvio + Mercado
  await page.evaluate(() => window.scrollBy(0, 400));
  await sleep(300);
  await shot('07-desvio-mercado');

  // 7. Sinais + Digest + News
  await page.evaluate(() => window.scrollBy(0, 400));
  await sleep(300);
  await shot('08-sinais-digest-news');

  // 8. Volta ao topo, abre TransactionModal
  await page.evaluate(() => window.scrollTo(0, 0));
  await sleep(300);
  const txButton = await page.evaluateHandle(() => {
    return [...document.querySelectorAll('button')].find((b) => b.textContent?.includes('+ Transação'));
  });
  await txButton.click();
  await sleep(800);
  await shot('09-modal-transacao');

  // fecha modal
  await page.keyboard.press('Escape');
  await page.evaluate(() => {
    const closeBtn = [...document.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Cancelar');
    closeBtn?.click();
  });
  await sleep(500);

  // 9. PriceChartModal — clicar em "Gráfico" na primeira linha
  await page.evaluate(() => window.scrollBy(0, 900));
  await sleep(500);
  const chartBtn = await page.evaluateHandle(() => {
    return [...document.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Gráfico');
  });
  if (chartBtn) {
    await chartBtn.click();
    await sleep(1500);
    await shot('10-modal-grafico');
    await page.keyboard.press('Escape');
    await page.evaluate(() => {
      const closeBtn = [...document.querySelectorAll('button')].find((b) => b.textContent?.trim() === '×');
      closeBtn?.click();
    });
    await sleep(500);
  }

  // 10. AportExecuteModal
  await page.evaluate(() => window.scrollTo(0, 0));
  await sleep(500);
  const execBtn = await page.evaluateHandle(() => {
    return [...document.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Executei');
  });
  if (execBtn) {
    await execBtn.click();
    await sleep(800);
    await shot('11-modal-aporte-executar');
    await page.keyboard.press('Escape');
  }

  console.log('OK');
} catch (e) {
  console.error('ERRO', e);
  process.exitCode = 1;
} finally {
  await browser.close();
}
