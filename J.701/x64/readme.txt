J.SessionManager.dll - is the PInvoke wrapper.
It depends on jsm.dll which in turn depends on j.dll

J.Console.exe - is the .NET implementation of JConsole.
jsmtest.exe - is a C implementation of JConsole but using jsm.dll wrapper.
J.SessionManager.Test.exe - is a naive implementation of JConsole
functionality using .NET Windows Form. For testing purposes.