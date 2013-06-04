README					Last updated: 2013-06


Introduction
------------

This directory (msvc) serves as a central location for all things needed to build the mono runtime using Microsoft Visual Studio.

BUILDING
------------

The "mono.sln" solution file contains the Visual C++ projects files to build some of the core mono libraries written in C. Note that the first time you open the solution mono.sln, Visual Studio may take a few tens of seconds parsing header files and creating a 'sdf' database of a substantial size.

* Embedded Samples
** teste.vcxproj
** test-invoke.vcxproj
** test-metadata.vcxproj
* Libraries
** eglib.vcxproj
** libmonoruntime.vcxproj
** libgc.vcxproj
** libmono.vcxproj
** libmonoutils.vcxproj
** monoposixhelper.vcxproj
* Profilers
** profiler-codeanalyst.vcxproj
** profiler-cov.vcxproj
** profiler-logging.vcxproj
** profiler-vtune.vcxproj
* Tests
** test_eglib.vcxproj
** libtest.vcxproj
* Tools
** genmdesc.vcxproj
** monodiet.vcxproj
** monodis.vcxproj
** monograph.vcxproj
** pedump.vcxproj

* mono.vcxproj

The solution contains 4 build configurations, and for each you can have either 32 or 64 bits builds.

### Building mono-2.0.dll

If what you are after is the core mono runtime library, for instance because you want to debug a hosted (embedded) Mono runtime in another application, you only need to build the project 'libmono' in the solution. Note that you can benefit of parallel builds via Tools -> Options... and Projects and Solutions / Build and Run options panel.

> Build: 4 succeeded, 2 failed, 0 up-to-date, 0 skipped


Known issues and troubleshooting
----------------

### error C2143 on a call to __readfsdword in libmonoruntime

libmonoruntime fails to compile on a call to __readfsdword in thread.c

Solution configuration is Debug, platform is Win32 or x64

Noticed the issue on the master branch as of 2013-06-03
The compilation of libmonoruntime fails with the following error as the first:

> mono\mono\metadata\threads.c(847): error C2143: syntax error : missing ';' before 'type'

which is a line:

>	void* tib = (void*)__readfsdword(0x18);

In Visual Studio going to the definition of the function points to the file.
c:\Program Files (x86)\Windows Kits\8.0\Include\um\winnt.h

#### Resolution

Not known.











	From this directory type:

	     msbuild.exe mono.sln /p:Configuration=Debug_eglib

	msbuild must be in your path, it comes with the .NET Framework.

MAINTENANCE
------------

	When new exported API calls are added to the runtime, issue the
	command:

		make update-def

	in this directory and commit the resulting mono.def file.

	This must happen on a Linux system, because we get the list of
	the exported symbols from the generated shared library.

