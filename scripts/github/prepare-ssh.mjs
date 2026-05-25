import { execFileSync } from "node:child_process";
import { chmodSync, mkdirSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";

const host = requireEnv("DROPLET_HOST");
const rawKey = requireEnv("DROPLET_SSH_KEY");
const sshDir = join(homedir(), ".ssh");
const keyPath = join(sshDir, "deploy_key");
const knownHostsPath = join(sshDir, "known_hosts");

mkdirSync(sshDir, { mode: 0o700, recursive: true });
chmodSync(sshDir, 0o700);

writeFileSync(keyPath, `${normalizePrivateKey(rawKey)}\n`, { encoding: "utf8", mode: 0o600 });
chmodSync(keyPath, 0o600);

try {
  execFileSync("ssh-keygen", ["-y", "-f", keyPath], { stdio: "ignore" });
} catch {
  console.error("::error::DROPLET_SSH_KEY is not a valid private key as seen by the runner.");
  process.exit(1);
}

const knownHost = execFileSync("ssh-keyscan", ["-T", "10", "-H", host], {
  encoding: "utf8",
  stdio: ["ignore", "pipe", "inherit"]
});
writeFileSync(knownHostsPath, knownHost, { encoding: "utf8", flag: "a", mode: 0o600 });

function normalizePrivateKey(value) {
  let key = value.trim();

  if (key.length >= 2 && key[0] === key[key.length - 1] && ["'", '"'].includes(key[0])) {
    key = key.slice(1, -1).trim();
  }

  if (key.includes("\\n") && !key.includes("\n")) {
    key = key.replaceAll("\\n", "\n");
  }

  return key.replaceAll("\r\n", "\n").replaceAll("\r", "\n");
}

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    console.error(`::error::${name} is required`);
    process.exit(1);
  }

  return value;
}
