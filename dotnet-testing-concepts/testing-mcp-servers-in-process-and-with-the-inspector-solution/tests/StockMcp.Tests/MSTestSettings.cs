// Every test builds its own server and client and shares nothing, so the tests may run in parallel.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
