CONFIG = Release
TARGET_FRAMEWORK = net6.0
RIDS = osx-x64 linux-x64
ARTIFACTSDIR = bin/
BINARIES = $(addprefix $(ARTIFACTSDIR),$(addsuffix /nuget,$(RIDS)))

.PHONY: all
all: $(BINARIES)

$(BINARIES):
	$(eval RID=$(notdir $(patsubst %/,%,$(dir $@))))
	dotnet publish \
		driver/driver.csproj \
		-c $(CONFIG) \
		-r $(RID) \
		--self-contained
	mkdir -p $(dir $@)
	cp driver/bin/$(CONFIG)/$(TARGET_FRAMEWORK)/$(RID)/publish/nuget $@

.PHONY: clean
clean:
	find . -type d \( -name bin -or -name obj \) -maxdepth 2 | xargs rm -rf
