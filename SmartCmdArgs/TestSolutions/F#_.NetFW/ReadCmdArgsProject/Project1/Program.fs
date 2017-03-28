// Weitere Informationen zu F# unter "http://fsharp.org".
// Weitere Hilfe finden Sie im Projekt "F#-Tutorial".

[<EntryPoint>]
let main argv = 
    let argString = String.concat " " argv
    System.IO.File.WriteAllText("../../../CmdLineArgs.txt", argString)
    0 // Integer-Exitcode zurückgeben
