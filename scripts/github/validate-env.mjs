const requiredNames = process.argv.slice(2);

if (requiredNames.length === 0) {
  console.error("Usage: node scripts/github/validate-env.mjs ENV_NAME...");
  process.exit(2);
}

const missingNames = requiredNames.filter((name) => !process.env[name]);

for (const name of missingNames) {
  console.error(`::error::${name} is required`);
}

process.exit(missingNames.length > 0 ? 1 : 0);
