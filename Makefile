MKBUNDLE = mkbundle
MKBUNDLE_VERSION = 6.8.0

MAC_TARGET = mono-$(MKBUNDLE_VERSION)-osx-10.9-x64
UBUNTU_TARGET = mono-$(MKBUNDLE_VERSION)-ubuntu-16.04-x64

TARGET_NAMES = $(MAC_TARGET) $(UBUNTU_TARGET)
TARGET_FILES_PATH = $(HOME)/.mono/targets/
TARGET_MONO_BINARIES = $(foreach TARGET_NAME,$(TARGET_NAMES),$(TARGET_FILES_PATH)$(TARGET_NAME)/bin/mono)

NUGET_WRAPPER_BIN_DIR = wrapper/bin/Debug/net48/
NUGET_WRAPPER_EXE = $(NUGET_WRAPPER_BIN_DIR)wrapper.exe
NUGET_EXE = $(NUGET_WRAPPER_BIN_DIR)NuGet.exe
NUGET_WRAPPER_SOURCES = $(wildcard wrapper/*.* patcher/*.*)

all: bin/nuget.mac bin/nuget.ubuntu

$(NUGET_WRAPPER_EXE): $(NUGET_WRAPPER_SOURCES)
	msbuild /r wrapper/wrapper.csproj

$(TARGET_MONO_BINARIES):
	$(MKBUNDLE) -v --fetch-target $(notdir $(patsubst %/,%,$(dir $(patsubst %/,%,$(dir $@)))))

.PHONY: .deps
.deps: $(NUGET_WRAPPER_EXE) $(TARGET_MONO_BINARIES)

define mkbundle_nuget
	mkdir -p bin/$(1)
	$(MKBUNDLE) -v \
		--simple \
		--cross $(2) \
		--config lib/config \
		-L lib/$(MKBUNDLE_VERSION) \
		-L $(NUGET_WRAPPER_BIN_DIR) \
		$(foreach lib,$(3),--library lib/$(MKBUNDLE_VERSION)/$(lib)) \
		$(4) \
		$(NUGET_WRAPPER_EXE) \
		-o bin/$(1)/nuget
endef

bin/nuget.mac: .deps
	$(call mkbundle_nuget,mac,$(MAC_TARGET),libmono-native-compat.dylib)

bin/nuget.ubuntu: .deps
	$(call mkbundle_nuget,ubuntu,$(UBUNTU_TARGET),libmono-native.so libmono-btls-shared.so,--library $(TARGET_FILES_PATH)$(UBUNTU_TARGET)/lib/libMonoPosixHelper.so)

.PHONY: clean
clean:
	rm -rf bin obj
	rm -rf wrapper/bin wrapper/obj
	rm -rf patcher/bin patcher/obj