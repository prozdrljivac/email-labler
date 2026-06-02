import { chmodSync, mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const deployDir = ".deploy";
const envNames = [
  "GMAIL_CLIENT_ID",
  "GMAIL_CLIENT_SECRET",
  "GMAIL_REFRESH_TOKEN",
  "GMAIL_USER_EMAIL",
  "PUBSUB_SERVICE_ACCOUNT_EMAIL",
  "PUBSUB_TOPIC_NAME",
  "DOMAIN",
  "LETSENCRYPT_EMAIL"
];

// Optional vars: written to .env only when the corresponding secret is set.
const optionalEnvNames = ["SENTRY_DSN"];

mkdirSync(deployDir, { mode: 0o700, recursive: true });
chmodSync(deployDir, 0o700);

const lines = envNames.map((name) => `${name}=${requireEnv(name)}`);
for (const name of optionalEnvNames) {
  const value = process.env[name];
  if (value) {
    lines.push(`${name}=${value}`);
  }
}

const envFile = join(deployDir, ".env");
writeFileSync(envFile, lines.join("\n") + "\n", { encoding: "utf8", mode: 0o600 });
chmodSync(envFile, 0o600);

const configFile = join(deployDir, "config.yaml");
writeFileSync(configFile, `${requireEnv("CONFIG_YAML").trim()}\n`, {
  encoding: "utf8",
  mode: 0o600
});
chmodSync(configFile, 0o600);

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    console.error(`::error::${name} is required`);
    process.exit(1);
  }

  return value;
}
