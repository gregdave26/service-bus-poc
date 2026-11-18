module.exports = {
  default: {
    paths: ['test/features/**/*.feature'],
    import: ['test/steps/**/*.steps.js'],
    tags: process.env.BDD_TAGS || 'not @wip',
    format: ['progress-bar', 'html:test-results/cucumber-report.html'],
    formatOptions: { snippetInterface: 'async-await' },
    timeout: 60000  // 60 seconds per step - navigation can be slow
  }
};
