"""
ML Dataset Preparation Script  (v2 – expanded & realistic)
- Generates a large, diverse pharmaceutical packaging-line dataset
- Preserves original real data from Excel files if present
- Creates realistic synthetic data across many operating scenarios
- Balanced classes: Normal / Warning / Critical
- Target: ~5 000 rows per class = ~15 000 total
- Saves OEE_dataset.csv in this folder (ML/)
"""

import os
import pandas as pd
import numpy as np

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
SOLUTION_DIR = os.path.join(SCRIPT_DIR, "..", "..")
DS1_PATH = os.path.join(SOLUTION_DIR, "OEE_Dataset.xlsx")
DS2_PATH = os.path.join(SOLUTION_DIR, "OeeReport_20260219082547.xlsx")
OUT_PATH = os.path.join(SCRIPT_DIR, "OEE_dataset.csv")

TARGET_PER_CLASS = 5000

NORMAL_MIN = 60.0
WARNING_MIN = 30.0

def classify(oee):
    if oee >= NORMAL_MIN:
        return "Normal"
    elif oee >= WARNING_MIN:
        return "Warning"
    return "Critical"


# ── Load real data if available ───────────────────────────────────────────────
real_parts = []
for path in [DS1_PATH, DS2_PATH]:
    if os.path.exists(path):
        try:
            df = pd.read_excel(path)
            print(f"Loaded {path}: {len(df)} rows")
            real_parts.append(df)
        except Exception as e:
            print(f"Warning: Could not load {path}: {e}")

FEATURE_COLS = ["Availability %", "Performance %", "Actual", "Packs",
                "Stop", "Start", "Processing", "Printing", "Idle"]
LABEL_REG = "OEE %"

real_df = pd.DataFrame()
if real_parts:
    raw = pd.concat(real_parts, ignore_index=True)
    if all(c in raw.columns for c in FEATURE_COLS + [LABEL_REG]):
        real_df = raw[FEATURE_COLS + [LABEL_REG]].copy()
        for col in real_df.columns:
            real_df[col] = pd.to_numeric(real_df[col], errors="coerce").fillna(0.0)
        real_df.rename(columns={
            "Availability %": "Availability",
            "Performance %":  "Performance",
            "OEE %":          "OEE",
        }, inplace=True)
        real_df["State"] = real_df["OEE"].apply(classify)
        print(f"\nReal data: {len(real_df)} rows")
        print(real_df["State"].value_counts().to_string())

# ── Scenario-based synthetic generation ───────────────────────────────────────
rng = np.random.default_rng(seed=42)

SCENARIOS = [
    # Normal scenarios (OEE >= 60)
    {"name": "peak_production",     "avail": (90, 100), "perf": (88, 100), "weight": 1.2},
    {"name": "steady_good",         "avail": (80, 95),  "perf": (75, 92),  "weight": 1.5},
    {"name": "good_morning_shift",  "avail": (82, 98),  "perf": (78, 95),  "weight": 1.0},
    {"name": "post_maintenance_ok", "avail": (75, 90),  "perf": (80, 95),  "weight": 0.8},
    {"name": "normal_afternoon",    "avail": (78, 93),  "perf": (72, 90),  "weight": 1.0},

    # Warning scenarios (OEE 30-60)
    {"name": "degrading_speed",     "avail": (70, 88),  "perf": (40, 65),  "weight": 1.0},
    {"name": "frequent_micro_stop", "avail": (50, 72),  "perf": (55, 78),  "weight": 1.2},
    {"name": "material_jam",        "avail": (55, 75),  "perf": (45, 70),  "weight": 1.0},
    {"name": "warmup_period",       "avail": (60, 80),  "perf": (42, 62),  "weight": 0.8},
    {"name": "shift_changeover",    "avail": (45, 68),  "perf": (60, 82),  "weight": 1.0},
    {"name": "minor_maint_needed",  "avail": (58, 78),  "perf": (48, 68),  "weight": 1.0},

    # Critical scenarios (OEE < 30)
    {"name": "major_breakdown",     "avail": (5, 30),   "perf": (10, 40),  "weight": 1.2},
    {"name": "quality_crisis",      "avail": (40, 65),  "perf": (15, 38),  "weight": 1.0},
    {"name": "startup_failure",     "avail": (8, 25),   "perf": (20, 50),  "weight": 0.8},
    {"name": "severe_jam",          "avail": (15, 40),  "perf": (18, 45),  "weight": 1.0},
    {"name": "end_of_life_tooling", "avail": (20, 50),  "perf": (12, 35),  "weight": 1.0},
    {"name": "power_issue",         "avail": (3, 20),   "perf": (25, 55),  "weight": 0.8},
]


def generate_scenario_rows(n, avail_range, perf_range):
    avail = rng.uniform(*avail_range, n)
    perf = rng.uniform(*perf_range, n)
    oee = avail * perf / 100.0

    actual = avail / 100.0
    packs = rng.integers(0, 55, n).astype(float)

    stop_base = np.clip((100 - avail) / 100.0, 0, 1)
    stop = stop_base * rng.uniform(0.4, 1.6, n)
    stop = np.clip(stop, 0, 0.85)

    start = rng.uniform(0, 0.08, n)
    proc = rng.uniform(0, 0.06, n)
    printing = actual * rng.uniform(0.85, 1.0, n)
    idle = np.clip(1.0 - printing - stop, 0, 1)

    noise_avail = rng.normal(0, 1.5, n)
    noise_perf = rng.normal(0, 1.5, n)
    avail = np.clip(avail + noise_avail, 0, 100)
    perf = np.clip(perf + noise_perf, 0, 100)
    oee = avail * perf / 100.0

    return pd.DataFrame({
        "Availability": np.round(avail, 4),
        "Performance":  np.round(perf, 4),
        "Actual":       np.round(actual, 4),
        "Packs":        packs,
        "Stop":         np.round(stop, 4),
        "Start":        np.round(start, 4),
        "Processing":   np.round(proc, 4),
        "Printing":     np.round(printing, 4),
        "Idle":         np.round(idle, 4),
        "OEE":          np.round(oee, 4),
        "State":        [classify(o) for o in oee],
    })


total_weight = sum(s["weight"] for s in SCENARIOS)
synth_parts = []
rows_per_unit = (TARGET_PER_CLASS * 3) / total_weight

for sc in SCENARIOS:
    n = int(rows_per_unit * sc["weight"])
    print(f"  Generating {n:>5} rows for scenario '{sc['name']}'")
    synth_parts.append(generate_scenario_rows(n, sc["avail"], sc["perf"]))

synth_df = pd.concat(synth_parts, ignore_index=True)
print(f"\nSynthetic data: {len(synth_df)} rows")
print(synth_df["State"].value_counts().to_string())

# ── Combine real + synthetic ──────────────────────────────────────────────────
if len(real_df) > 0:
    df_all = pd.concat([real_df, synth_df], ignore_index=True)
else:
    df_all = synth_df

# ── Balance classes ───────────────────────────────────────────────────────────
balanced_parts = []
for state in ["Normal", "Warning", "Critical"]:
    subset = df_all[df_all["State"] == state]
    current = len(subset)
    if current >= TARGET_PER_CLASS:
        balanced_parts.append(subset.sample(n=TARGET_PER_CLASS, random_state=42))
    else:
        needed = TARGET_PER_CLASS - current
        avail_range = {"Normal": (70, 100), "Warning": (45, 78), "Critical": (5, 45)}[state]
        perf_range = {"Normal": (70, 100), "Warning": (40, 72), "Critical": (8, 45)}[state]
        extra = generate_scenario_rows(needed, avail_range, perf_range)
        extra["State"] = state
        balanced_parts.append(pd.concat([subset, extra], ignore_index=True))
        print(f"  Topped up '{state}': {current} → {TARGET_PER_CLASS}")

df_final = pd.concat(balanced_parts, ignore_index=True)
df_final = df_final.sample(frac=1, random_state=42).reset_index(drop=True)

print(f"\n{'='*50}")
print(f"Final dataset: {len(df_final)} rows")
print(f"\nClass distribution:")
print(df_final["State"].value_counts().to_string())
print(f"\nOEE stats:")
print(f"  min  = {df_final['OEE'].min():.2f}")
print(f"  max  = {df_final['OEE'].max():.2f}")
print(f"  mean = {df_final['OEE'].mean():.2f}")
print(f"  std  = {df_final['OEE'].std():.2f}")
print(f"\nFeature ranges:")
for col in ["Availability", "Performance", "Actual", "Packs", "Stop", "Start", "Processing", "Printing", "Idle"]:
    print(f"  {col:15s}: [{df_final[col].min():.4f}, {df_final[col].max():.4f}]  mean={df_final[col].mean():.4f}")

# ── Save ──────────────────────────────────────────────────────────────────────
df_final.to_csv(OUT_PATH, index=False, float_format="%.4f")
print(f"\nSaved → {OUT_PATH}")
print("Done. Run 'Train models' from the web app.")
