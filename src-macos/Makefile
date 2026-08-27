# CRT (macOS) — convenience wrapper around SwiftPM.
#
#   make            build the debug binary
#   make test       run the CRTCoreTests suite
#   make release    build the optimized binary
#   make app        assemble build/CRT.app
#   make run        assemble build/CRT.app and launch it
#   make clean      remove SwiftPM and bundle build output

SWIFT ?= swift

.PHONY: build test release app run clean

build:
	$(SWIFT) build

test:
	$(SWIFT) test

release:
	$(SWIFT) build -c release

app:
	bash Scripts/make-app.sh

run: app
	open build/CRT.app

clean:
	$(SWIFT) package clean
	rm -rf build
