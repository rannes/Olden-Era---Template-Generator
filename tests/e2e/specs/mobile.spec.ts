/**
 * T-301 — Mobile (iPhone-SE-class) regression net.
 *
 * The desktop layout has a sticky preview column on the right with Generate
 * at the top, so it's always in viewport. At narrow widths the grid collapses
 * to a single column and the preview column sits at the bottom of the page,
 * which used to push Generate off-screen entirely. The fix pins the action
 * bar to the viewport bottom on ≤600px. This test exercises that path.
 */
import { test, expect } from '@playwright/test';

test.describe('Mobile layout @ 375×667', () => {
  test.use({ viewport: { width: 375, height: 667 } });

  test('Generate is reachable in viewport without scrolling', async ({ page }) => {
    await page.goto('/');

    const generate = page.getByRole('button', { name: /Generate Template/i });
    await expect(generate).toBeVisible();

    // Without scrolling, the button must intersect the visible viewport.
    const box = await generate.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.y).toBeGreaterThanOrEqual(0);
    expect(box!.y + box!.height).toBeLessThanOrEqual(667);
  });

  test('Generate then Download map produces a .zip', async ({ page }) => {
    await page.goto('/');

    await page.getByRole('button', { name: /Generate Template/i }).click();
    await expect(page.locator('.oe-preview img')).toBeVisible({ timeout: 60_000 });

    // Open the preview details if mobile collapsed it (it's open by default,
    // but be defensive in case future changes flip the default).
    const summary = page.locator('.oe-preview-summary');
    if (await summary.isVisible()) {
      const details = page.locator('.oe-preview-details');
      const isOpen = await details.evaluate(el => (el as HTMLDetailsElement).open);
      if (!isOpen) await summary.click();
    }

    const downloadButton = page.getByRole('button', { name: /^Download map$/i });
    await downloadButton.scrollIntoViewIfNeeded();
    await expect(downloadButton).toBeEnabled();

    const [download] = await Promise.all([
      page.waitForEvent('download'),
      downloadButton.click(),
    ]);

    expect(download.suggestedFilename()).toMatch(/\.zip$/);
  });

  test('preview can be collapsed via the summary toggle', async ({ page }) => {
    await page.goto('/');

    const summary = page.locator('.oe-preview-summary');
    await expect(summary).toBeVisible();

    const preview = page.locator('.oe-preview');
    await expect(preview).toBeVisible();

    await summary.click();
    await expect(preview).toBeHidden();

    await summary.click();
    await expect(preview).toBeVisible();
  });
});
