import json
import sys


def solve(cart):
    subtotal = 0
    units = 0
    free = []
    error = None
    for line in cart["lines"]:
        if line["qty"] < 1:
            error = "invalid quantity for sku: " + line["sku"]
            continue
        subtotal += line["qty"] * line["unitCents"]
        units += line["qty"]
        if line["unitCents"] == 0:
            free.append(line["sku"])
    if error is not None:
        return {"ok": False, "error": error}
    return {"ok": True, "value": {"subtotalCents": subtotal, "unitCount": units, "freeSkus": free}}


print(json.dumps(solve(json.load(sys.stdin))))
