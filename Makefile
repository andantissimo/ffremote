PREFIX        ?= /usr/local
BINDIR        ?= $(PREFIX)/bin
SYSCONFDIR    ?= $(PREFIX)/etc
SERVICE_USER  ?= nobody
SERVICE_PORT  ?= 5255
CONFIGURATION ?= Release

ifeq ($(shell uname),Darwin)
	RID ?= osx-x64
else ifeq ($(shell uname),FreeBSD)
	RID ?= freebsd-x64
else
	RID ?= linux-x64
endif
ifneq ($(findstring freebsd,$(RID)),)
	PUBLISH_OPT := -p:PublishSingleFile=true
	SERVICE_EXT := rc
else ifneq ($(findstring osx,$(RID)),)
	PUBLISH_OPT := -p:PublishSingleFile=true -p:PublishTrimmed=true
	SERVICE_EXT := plist
else
	PUBLISH_OPT := -p:PublishSingleFile=true -p:PublishTrimmed=true
	SERVICE_EXT := service
endif

PROJECT          := $(shell perl -ne '/"([^"]+\.csproj)"/ && print $$1 =~ s:\\:/:r' *.sln)
PROJECT_DIR      := $(shell dirname $(PROJECT))
PROJECT_NAME     := $(shell basename $(PROJECT) .csproj)
TARGET_FRAMEWORK := $(shell perl -ne '/<(TargetFramework)>(.*)<\/\1>/ && print $$2' $(PROJECT))
ASSEMBLY_NAME    := $(shell perl -ne '/<(AssemblyName)>(.*)<\/\1>/ && print $$2' $(PROJECT))
ifeq ($(ASSEMBLY_NAME),)
	ASSEMBLY_NAME = $(PROJECT_NAME)
endif

SRCS   := $(shell find $(PROJECT_DIR) -name *.cs) $(PROJECT)
OUTDIR := $(PROJECT_DIR)/bin/$(CONFIGURATION)/$(TARGET_FRAMEWORK)/$(RID)/publish

.PHONY: all install clean

all: $(OUTDIR)/$(ASSEMBLY_NAME) $(OUTDIR)/$(ASSEMBLY_NAME).$(SERVICE_EXT)

install: all
	install -d -m 755 $(SYSCONFDIR)/$(ASSEMBLY_NAME)
	install -m 644 $(OUTDIR)/appsettings.json $(SYSCONFDIR)/$(ASSEMBLY_NAME)/appsettings.json
	install -m 755 $(OUTDIR)/$(ASSEMBLY_NAME) $(BINDIR)/$(ASSEMBLY_NAME)
ifneq ($(findstring freebsd,$(RID)),)
	install -m 755 $(OUTDIR)/$(ASSEMBLY_NAME).rc /usr/local/etc/rc.d/$(ASSEMBLY_NAME)
else ifneq ($(findstring osx,$(RID)),)
	install -m 644 -o $(SERVICE_USER) /dev/null /var/log/$(ASSEMBLY_NAME).log 
	install -m 644 $(OUTDIR)/$(ASSEMBLY_NAME).plist /Library/LaunchDaemons/$(ASSEMBLY_NAME).plist
else
	install -m 644 $(OUTDIR)/$(ASSEMBLY_NAME).service /etc/systemd/system/$(ASSEMBLY_NAME).service
endif

clean:
	$(RM) -r $(PROJECT_DIR)/bin $(PROJECT_DIR)/obj

$(OUTDIR)/$(ASSEMBLY_NAME): $(SRCS)
	dotnet publish --nologo -c $(CONFIGURATION) $(PUBLISH_OPT) -r $(RID) --sc

$(OUTDIR)/$(ASSEMBLY_NAME).$(SERVICE_EXT): $(ASSEMBLY_NAME).$(SERVICE_EXT).in
	mkdir -p $(OUTDIR)
	cat $(ASSEMBLY_NAME).$(SERVICE_EXT).in \
	  | sed -e 's:@ASSEMBLY_NAME@:$(ASSEMBLY_NAME):' \
	        -e 's:@BINDIR@:$(BINDIR):' \
	        -e 's:@SYSCONFDIR@:$(SYSCONFDIR):' \
	        -e 's:@USER@:$(SERVICE_USER):' \
	        -e 's:@PORT@:$(SERVICE_PORT):' \
	  > $@
