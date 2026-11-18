import { Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';

Then('a Continue or Make Payment button should be visible', async function() {
  const actionButton = this.page
    .locator('button:has-text("Continue"), button:has-text("Make Payment")')
    .first();
  await expect(actionButton).toBeVisible();
});

Then('the page title should contain {string}', async function(expectedText) {
  const title = await this.page.title();
  expect(title).toContain(expectedText);
});

Then('at least one heading should be visible', async function() {
  await expect(this.page.locator('h1, h2').first()).toBeVisible();
});
