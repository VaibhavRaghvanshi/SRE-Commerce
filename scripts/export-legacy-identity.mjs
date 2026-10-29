import { DatabaseSync } from 'node:sqlite';
import { writeFileSync, existsSync } from 'node:fs';
import { resolve, sep } from 'node:path';

const [sourcePath, outputPath] = process.argv.slice(2);
if (!sourcePath || !outputPath) throw new Error('Supply a verified SQLite backup path and a new ignored artifacts JSON path.');
const output = resolve(outputPath);
if (!output.startsWith(resolve('artifacts') + sep) || existsSync(output)) throw new Error('Output must be a new path under ignored artifacts; refusing overwrite.');
const db = new DatabaseSync(resolve(sourcePath), { readOnly: true });
const users = db.prepare('SELECT Id, FullName, Email, PasswordHash, Role FROM Users ORDER BY Id').all();
if (new Set(users.map(u => u.Email.trim().toUpperCase())).size !== users.length) throw new Error('Duplicate normalized emails require review.');
writeFileSync(output, JSON.stringify({ source: 'legacy-sqlite', users }, null, 2));
db.close();
console.log(JSON.stringify({ exportedUsers: users.length, destination: output, credentialsPrinted: false }));
