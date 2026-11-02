// Portable baseline integrity check. No network or local evidence required.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),assert=require('assert/strict');
const hashes=JSON.parse(fs.readFileSync(path.join(__dirname,'baseline.sha256.json'),'utf8'));
for(const [file,expected] of Object.entries(hashes))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(path.join(__dirname,file))).digest('hex'),expected,file);
console.log(JSON.stringify({baselineFilesVerified:Object.keys(hashes).length,inferenceRequests:0}));
