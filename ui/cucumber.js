export default {
  default: {
    import: ['test/steps/**/*.steps.js'],
    format: ['progress-bar', 'html:test-results/cucumber-report.html'],
    formatOptions: { snippetInterface: 'async-await' },
    timeout: 30000  // 30 seconds per step
  }
};
