# Native asset staging

This directory contains only process documentation in M0. Future tooling must download AMD artifacts from official fixed-version URLs, verify SHA-256 hashes, resolve the native dependency closure, and stage files outside Git before packing them under `runtimes/<rid>/native`.

The `downloads`, `staging`, and `cache` subdirectories are ignored by Git.
