{
  "default": {
    "require": ["test/steps/**/*.steps.js"],
    "format": ["progress-bar", "html:test-results/cucumber-report.html"],
    "formatOptions": { "snippetInterface": "async-await" }
  }
}
