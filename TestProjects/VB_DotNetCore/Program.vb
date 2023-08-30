Module Program
    Sub Main(args As String())
        ' Display command line arguments
        Console.WriteLine("Command Line Arguments:")
        For Each arg As String In args
            Console.WriteLine(arg)
        Next

        ' Display environment variables
        Console.WriteLine(vbCrLf & "Environment Variables:")
        For Each key As String In Environment.GetEnvironmentVariables().Keys
            Console.WriteLine($"{key} = {Environment.GetEnvironmentVariable(key)}")
        Next
    End Sub
End Module
