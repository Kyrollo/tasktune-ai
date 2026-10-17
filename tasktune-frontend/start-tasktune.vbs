Set WshShell = CreateObject("WScript.Shell")
WshShell.Run "cmd /c cd /d ""D:\Personal\Courses\Agentic AI - NTI\Final Project\files\tasktune-frontend"" && npx serve dist", 0, False
WScript.Sleep 2000
WshShell.Run "http://localhost:3000"