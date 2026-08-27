import json
import sys


def solve(customer):
    primary = customer["primary"]
    if primary["kind"] == "email":
        return {"ok": True, "value": {"target": primary["address"], "viaBackup": False}}
    backup = customer["backup"]
    if backup is not None and backup["kind"] == "email":
        return {"ok": True, "value": {"target": backup["address"], "viaBackup": True}}
    return {"ok": False, "error": "no email contact for " + customer["name"]}


print(json.dumps(solve(json.load(sys.stdin))))
