open System

[<EntryPoint>]
let main argv =
    // Display command line arguments
    printfn "Command Line Arguments:"
    argv |> Array.iter (printfn "%s")

    // Display environment variables
    printfn "\nEnvironment Variables:"
    Environment.GetEnvironmentVariables() 
    |> Seq.cast<System.Collections.DictionaryEntry>
    |> Seq.iter (fun de -> printfn "%s = %s" (de.Key.ToString()) (de.Value.ToString()))

    0 // Return an integer exit code
