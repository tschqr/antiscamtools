CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

all: stree streecw streecwdbg

stree: stree.cs parentprocessutilities.cs
	$(CSC) /nologo /warn:4 $^

streecw: streecw.cs streecwform.cs
	$(CSC) /nologo /warn:4 /target:winexe $^

streecwdbg: streecw.cs streecwform.cs
	$(CSC) /nologo /warn:4 /out:$@.exe $^
