var argString = "";
process.argv.forEach((val, index, array) => {
    if (index > 2)
        argString += " ";
    if (index > 1)
        argString += val;
});
var fs = require("fs");
fs.writeFile("../CmdLineArgs.txt", argString, err => {
    if (err) {
        return console.log(err);
    }
    console.log("The file was saved!");
    process.exit(0);
}); 