import { test, expect } from '@playwright/test';

test('home page loads with header and Generate button', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toContainText(/Olden Era/i);
  await expect(page.getByRole('button', { name: /Generate Template/i })).toBeVisible();
});
