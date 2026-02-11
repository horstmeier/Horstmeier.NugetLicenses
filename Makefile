CONFIGURATION ?= Release
PROJECT        = src/Horstmeier.NugetLicenses/Horstmeier.NugetLicenses.csproj
TEST_PROJECT   = tests/Horstmeier.NugetLicenses.Tests/Horstmeier.NugetLicenses.Tests.csproj

.PHONY: all build test test-unit pack install uninstall clean restore

all: build

restore:
	dotnet restore

build: restore
	dotnet build -c $(CONFIGURATION) --no-restore

test: build
	dotnet test -c $(CONFIGURATION) --no-build

test-unit: build
	dotnet test -c $(CONFIGURATION) --no-build --filter "Category!=Integration"

pack: build
	dotnet pack $(PROJECT) -c $(CONFIGURATION) --no-build

install: pack
	dotnet tool install --global --add-source src/Horstmeier.NugetLicenses/bin/$(CONFIGURATION) Horstmeier.NugetLicenses

uninstall:
	dotnet tool uninstall --global Horstmeier.NugetLicenses

clean:
	dotnet clean -c $(CONFIGURATION)
	rm -rf src/Horstmeier.NugetLicenses/bin src/Horstmeier.NugetLicenses/obj
	rm -rf tests/Horstmeier.NugetLicenses.Tests/bin tests/Horstmeier.NugetLicenses.Tests/obj
