Module Program
    Sub Main(args As String())
        ' Display command line arguments
        Console.WriteLine("Command Line Arguments:")
        For Each arg As String In args
            Console.WriteLine(arg)
        Next
    End Sub
End Module
