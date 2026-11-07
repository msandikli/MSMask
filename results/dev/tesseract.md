### tesseract

Dataset: `data/dev/labels.json` (150 images), IoU >= 0.5, protection threshold 95%

| Kind | TP | FP | FN | Precision | Recall | F1 | Protection |
|---|---:|---:|---:|---:|---:|---:|---:|
| TCKN | 124 | 0 | 4 | 1.000 | 0.969 | 0.984 | 0.969 (124/128) |
| TELEFON | 198 | 0 | 3 | 1.000 | 0.985 | 0.992 | 0.985 (198/201) |
| EPOSTA | 219 | 0 | 10 | 1.000 | 0.956 | 0.978 | 0.948 (217/229) |
| IBAN | 147 | 0 | 3 | 1.000 | 0.980 | 0.990 | 0.980 (147/150) |
| KREDI_KARTI | 149 | 0 | 2 | 1.000 | 0.987 | 0.993 | 0.987 (149/151) |
| API_ANAHTARI | 201 | 13 | 6 | 0.939 | 0.971 | 0.955 | 0.903 (187/207) |
| **Total (micro)** | 1038 | 13 | 28 | **0.988** | **0.974** | **0.981** | **0.959** |

Macro F1: 0.982 · Latency (ms): mean 864, median 803, p95 1471 · Stage means: lines 1, ocr 860, patterns 3
