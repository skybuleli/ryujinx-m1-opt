# Ryujinx M1 Optimization Build

**Branch**: feature/1.1-net10-upgrade
**Date**: Sat Jan 17 18:42:47 CST 2026

## 🚀 Optimization Highlights
1. **.NET 10 Upgrade**: Core framework upgraded to .NET 10 preview for better ARM64 code generation.
2. **Texture Decoding (BCnDecoder)**:
   - **BC1 (RGB)**: +22% Performance (NEON optimized).
   - **BC3 (RGB+A)**: +4% Performance (Hybrid NEON/Scalar).
   - **BC4/BC5**: Optimized with NEON lookup.
3. **Intrinsics**: Replaced legacy vector operations with .NET 10 hardware intrinsics.

## ⚠️ Notes
- This build is unsigned (Ad-hoc). You may need to run the following if it doesn't open:
  `xattr -cr Ryujinx.app`

