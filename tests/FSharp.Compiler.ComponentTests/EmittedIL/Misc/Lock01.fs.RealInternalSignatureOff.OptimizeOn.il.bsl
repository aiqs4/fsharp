




.assembly extern runtime { }
.assembly extern FSharp.Core { }
.assembly extern System.Threading
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )                         
  .ver 10:0:0:0
}
.assembly assembly
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.FSharpInterfaceDataVersionAttribute::.ctor(int32,
                                                                                                      int32,
                                                                                                      int32) = ( 01 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 ) 

  
  

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.module assembly.exe

.imagebase {value}
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       
.corflags 0x00000001    





.class public abstract auto ansi sealed assembly
       extends [runtime]System.Object
{
  .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 07 00 00 00 00 00 ) 
  .method public specialname static object get_o() cil managed
  {
    
    .maxstack  8
    IL_0000:  ldsfld     object '<StartupCode$assembly>'.$assembly::o@19
    IL_0005:  ret
  } 

  .method assembly specialname static bool get_lockTaken@1() cil managed
  {
    
    .maxstack  8
    IL_0000:  ldsfld     bool '<StartupCode$assembly>'.$assembly::lockTaken@1
    IL_0005:  ret
  } 

  .method assembly specialname static void set_lockTaken@1(bool 'value') cil managed
  {
    
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  stsfld     bool '<StartupCode$assembly>'.$assembly::lockTaken@1
    IL_0006:  ret
  } 

  .property object o()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .get object assembly::get_o()
  } 
  .property bool lockTaken@1()
  {
    .custom instance void [FSharp.Core]Microsoft.FSharp.Core.CompilationMappingAttribute::.ctor(valuetype [FSharp.Core]Microsoft.FSharp.Core.SourceConstructFlags) = ( 01 00 09 00 00 00 00 00 ) 
    .set void assembly::set_lockTaken@1(bool)
    .get bool assembly::get_lockTaken@1()
  } 
} 

.class private abstract auto ansi sealed '<StartupCode$assembly>'.$assembly
       extends [runtime]System.Object
{
  .field static assembly object o@19
  .custom instance void [runtime]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [runtime]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly bool lockTaken@1
  .custom instance void [runtime]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [runtime]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .field static assembly int32 init@
  .custom instance void [runtime]System.Diagnostics.DebuggerBrowsableAttribute::.ctor(valuetype [runtime]System.Diagnostics.DebuggerBrowsableState) = ( 01 00 00 00 00 00 00 00 ) 
  .custom instance void [runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [runtime]System.Diagnostics.DebuggerNonUserCodeAttribute::.ctor() = ( 01 00 00 00 ) 
  .method public static void  main@() cil managed
  {
    .entrypoint
    
    .maxstack  4
    IL_0000:  newobj     instance void [runtime]System.Object::.ctor()
    IL_0005:  stsfld     object '<StartupCode$assembly>'.$assembly::o@19
    IL_000a:  ldc.i4.0
    IL_000b:  stsfld     bool '<StartupCode$assembly>'.$assembly::lockTaken@1
    .try
    {
      IL_0014:  ldloc.1
      IL_0015:  ldloca.s   V_2
      IL_0017:  call       void [System.Threading]System.Threading.Monitor::Enter(object,
                                                                                  bool&)
      IL_001c:  leave.s    IL_0029

    }  
    finally
    {
      IL_0021:  call       bool assembly::get_lockTaken@1()
      IL_0026:  brfalse.s  IL_0033

      IL_0021:  ldloc.1
      IL_0022:  call       void [System.Threading]System.Threading.Monitor::Exit(object)
      IL_0027:  endfinally
      IL_0028:  endfinally
    }  
    IL_0034:  ret
  } 

} 





