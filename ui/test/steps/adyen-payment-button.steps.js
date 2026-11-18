import { When, Then } from '@cucumber/cucumber';
import { expect } from '@playwright/test';

const adyenPaymentForm = (page) => page.getByLabel('Adyen payment form');

When('I choose to pay annually by card', async function() {
  await this.page.getByRole('radio', { name: 'Card', exact: true }).check();
  await this.page.getByRole('radio', { name: /Annual/ }).check();
});

Then('the Adyen payment form container should be attached', async function() {
  await expect(adyenPaymentForm(this.page)).toBeAttached();
});

Then('the Adyen card component should be displayed', async function() {
  await expect(adyenPaymentForm(this.page)).toBeVisible();
});

Then('the Adyen pay button should be hidden', async function() {
  await expect(
    adyenPaymentForm(this.page).locator('.adyen-checkout__button--pay')
  ).toHaveCSS('display', 'none');
});
