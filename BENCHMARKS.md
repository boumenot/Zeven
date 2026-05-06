# Compression Benchmarks

Compression ratios and average times (10 runs) for each codec at default settings.

Test data from [google/snappy testdata](https://github.com/google/snappy/tree/main/testdata).

## Compression Ratio + Speed

| Dataset  | Original |      LZMA2      |      PPMd       |      Zstd       |     Brotli      |       LZ4       |
|----------|----------|-----------------|-----------------|-----------------|-----------------|-----------------|
| html     |  102,400 |  12.1% (19.9ms) |  11.3% ( 2.6ms) |  14.5% ( 2.0ms) |  13.9% ( 1.4ms) |  16.9% ( 1.1ms) |
| urls     |  712,086 |  22.2% (54.9ms) |  20.0% (29.9ms) |  25.8% ( 4.9ms) |  26.6% ( 6.0ms) |  34.9% ( 6.2ms) |
| jpg      |  123,093 | 100.0% (22.0ms) | 100.8% (19.0ms) | 100.1% ( 2.4ms) | 100.1% ( 1.4ms) | 100.1% ( 2.5ms) |
| pdf      |  102,400 |  79.0% (19.4ms) |  80.1% (13.0ms) |  80.7% ( 2.2ms) |  81.0% ( 1.4ms) |  80.3% ( 1.7ms) |
| html4    |  409,600 |   3.0% (33.4ms) |   5.8% ( 4.9ms) |   3.6% ( 2.2ms) |   3.5% ( 1.4ms) |  15.9% ( 1.9ms) |
| txt1     |  152,089 |  31.9% (29.6ms) |  25.6% ( 8.1ms) |  36.7% ( 2.5ms) |  37.4% ( 2.0ms) |  44.7% ( 2.0ms) |
| txt2     |  129,301 |  34.6% (26.6ms) |  28.1% ( 7.4ms) |  39.2% ( 2.4ms) |  39.5% ( 1.8ms) |  48.4% ( 1.6ms) |
| txt3     |  426,754 |  28.0% (51.6ms) |  22.6% (19.4ms) |  33.1% ( 3.6ms) |  34.4% ( 4.1ms) |  41.2% ( 4.4ms) |
| txt4     |  481,861 |  34.4% (63.5ms) |  27.5% (28.0ms) |  39.8% ( 4.2ms) |  41.1% ( 5.1ms) |  50.7% ( 6.1ms) |
| pb       |  118,588 |  10.2% (18.1ms) |  10.6% ( 2.7ms) |  11.9% ( 1.9ms) |  12.1% ( 1.2ms) |  13.4% ( 585µs) |
| gaviota  |  184,320 |  13.8% (37.6ms) |  18.5% ( 5.3ms) |  21.8% ( 1.9ms) |  21.6% ( 2.0ms) |  32.3% ( 1.9ms) |

Lower ratio = better compression. All codecs use default compression level.

## Speed Tiers

At default settings:

- **LZ4/Zstd/Brotli**: 1–6ms — 10–30× faster than LZMA2
- **PPMd**: 3–30ms — best text ratios at moderate speed
- **LZMA2**: 18–64ms — best binary ratios but slowest

## Observations

- **LZMA2** — best ratio on binary/mixed data (html4, pb, gaviota), but 10–30× slower than Zstd/LZ4.
- **PPMd** — best ratio on text (txt1–txt4, urls), moderate speed.
- **Zstd** — strong ratio/speed balance. Near-LZMA2 ratios at 10× the speed.
- **Brotli** — similar profile to Zstd, slightly better on some datasets.
- **LZ4** — fastest codec, weakest ratios. Good for throughput-sensitive workloads.
- **JPEG** is incompressible — all codecs produce ~100% output.

## Regenerate

```
dotnet run --project benchmarks\Zeven.Benchmarks -c Release -- --ratios
```
