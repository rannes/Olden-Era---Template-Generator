import { test, expect } from '@playwright/test';

test('home page loads with header and Generate button', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText(/Olden Era/i);
  await expect(page.getByRole('button', { name: /Generate Template/i })).toBeVisible();
});

test('Generate produces a preview image', async ({ page }) => {
  await page.goto('/');

  const generate = page.getByRole('button', { name: /Generate Template/i });
  await expect(generate).toBeEnabled();
  await generate.click();

  // Spinner may appear briefly. Then a PNG preview <img> with a data: URL.
  const preview = page.locator('.oe-preview img');
  await expect(preview).toBeVisible({ timeout: 60_000 });

  const src = await preview.getAttribute('src');
  expect(src).toMatch(/^data:image\/png;base64,[A-Za-z0-9+/=]+$/);
  // Sanity: payload is non-trivial (more than just a few bytes of base64).
  expect((src ?? '').length).toBeGreaterThan(1000);
});
