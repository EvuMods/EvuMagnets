.PHONY: fetch-refs build test verify package clean

ROOT := $(abspath $(dir $(lastword $(MAKEFILE_LIST))))
SOLUTION := $(ROOT)/EvuMagnets.sln

# A distro package can put a runtime-only `dotnet` ahead of a user-local SDK.
ifneq ($(wildcard $(HOME)/.dotnet/dotnet),)
export PATH := $(HOME)/.dotnet:$(PATH)
export DOTNET_ROOT := $(HOME)/.dotnet
endif

fetch-refs:
	bash "$(ROOT)/scripts/fetch-refs.sh"

build: fetch-refs
	dotnet build "$(SOLUTION)" -c Release

test: build
	dotnet test "$(SOLUTION)" -c Release --no-build

verify: test

package: build
	bash "$(ROOT)/scripts/package.sh"

clean:
	dotnet clean "$(SOLUTION)" -c Release
	rm -rf "$(ROOT)/dist"
