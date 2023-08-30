// Display command line arguments
console.log("Command Line Arguments:");
process.argv.forEach(arg => {
    console.log(arg);
});

// Display environment variables
console.log("\nEnvironment Variables:");
for (let key in process.env) {
    console.log(`${key} = ${process.env[key]}`);
}