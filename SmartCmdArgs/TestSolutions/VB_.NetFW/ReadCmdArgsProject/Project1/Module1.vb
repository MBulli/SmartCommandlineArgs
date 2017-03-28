Module Module1
    Sub Main()
        Dim output As String = String.Join(" ", My.Application.CommandLineArgs)
        My.Computer.FileSystem.WriteAllText("../../../CmdLineArgs.txt", output ,False)
    End Sub
End Module
