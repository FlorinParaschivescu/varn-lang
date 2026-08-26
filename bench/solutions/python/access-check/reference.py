import json
import sys


def solve(request):
    if request["role"].startswith("admin"):
        return {"allowed": True, "reason": "admin role"}
    age = request.get("age")
    if age is not None and age >= 18 and request["region"] == "EU":
        return {"allowed": True, "reason": "adult in region"}
    return {"allowed": False, "reason": "denied"}


print(json.dumps(solve(json.load(sys.stdin))))
