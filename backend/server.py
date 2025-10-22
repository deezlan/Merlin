import sys, os, json, tempfile, subprocess, shutil
from pathlib import Path
from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import Optional

# --- config ----
import sys
from pathlib import Path

BASE = Path(__file__).parent.resolve()
PYTHON_EXE = sys.executable      # ✅ use current venv
EXTRACT   = (BASE / "extraction.py").resolve()               # ✅ absolute
SUMMARIZE = (BASE / "summarize.py").resolve()
GENERATE  = (BASE / "generateQuestions.py").resolve()
GRADER    = (BASE / "grader.py").resolve()

assert Path(PYTHON_EXE).exists(), f"Bad PYTHON_EXE: {PYTHON_EXE}"
for p in (EXTRACT,SUMMARIZE,GENERATE,GRADER): assert p.exists(), f"Missing: {p}"

def run(cmd: list[str], cwd: Path = None):
    print(f"🚀 Running command: {' '.join(cmd)} in {cwd or os.getcwd()}")
    p = subprocess.run(
        cmd, cwd=str(cwd) if cwd else None,
        capture_output=True, text=True
    )

    # Always print stdout/stderr so you can see it in the terminal
    print(f"STDOUT:\n{p.stdout}")
    print(f"STDERR:\n{p.stderr}")

    if p.returncode != 0:
        # Raise with full context
        raise HTTPException(
            500,
            f"Command failed ({p.returncode}): {' '.join(cmd)}\n"
            f"STDOUT:\n{p.stdout}\n"
            f"STDERR:\n{p.stderr}"
        )
    return p.stdout

app = FastAPI(title="Wrapper for existing CLI scripts")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"], allow_methods=["*"], allow_headers=["*"], allow_credentials=True
)

# ---------- Schemas ----------
class SummarizeReq(BaseModel):
    text: str

class GenerateReq(BaseModel):
    summary: str

class GradeReq(BaseModel):
    summary: str
    full_text: str  # contains "Question: ... Student A: ... Student B: ..."

# ---------- Endpoints ----------

@app.post("/pipeline")
async def pipeline_endpoint(file: UploadFile = File(...)):
    """
    Upload PDF/PPTX once, run:
      extraction.py -> output.txt
      summarize.py  -> summarized.txt
      generateQuestions.py -> questions.txt
    Returns only: {"questions": "Q1\\nQ2...", "count": N}
    """
    name = file.filename or "upload"
    suffix = Path(name).suffix.lower()
    if suffix not in (".pdf", ".pptx"):
        raise HTTPException(400, f"Only .pdf or .pptx accepted (got: {name})")

    with tempfile.TemporaryDirectory() as td:
        td = Path(td)
        in_path  = td / f"input{suffix}"
        out_txt  = td / "output.txt"
        summ_txt = td / "summarized.txt"
        q_txt    = td / "questions.txt"

        in_path.write_bytes(await file.read())

        # 1️⃣ Extract
        run([PYTHON_EXE, str(EXTRACT), str(in_path)], cwd=td)
        if not out_txt.exists():
            raise HTTPException(500, "extraction.py didn't produce output.txt")

        # 2️⃣ Summarize
        run([PYTHON_EXE, str(SUMMARIZE), str(out_txt), str(summ_txt)], cwd=td)
        if not summ_txt.exists():
            raise HTTPException(500, "summarize.py didn't produce summarized.txt")

        # 3️⃣ Generate questions
        run([PYTHON_EXE, str(GENERATE), str(summ_txt), str(q_txt)], cwd=td)
        if not q_txt.exists():
            raise HTTPException(500, "generateQuestions.py didn't produce questions.txt")

        # Return only questions
        summary   = summ_txt.read_text(encoding="utf-8", errors="ignore").strip()
        questions = q_txt.read_text(encoding="utf-8", errors="ignore").strip()
        return {
            "summary": summary,
            "questions": questions,
            "count": len([ln for ln in questions.splitlines() if ln.strip()])
        }

@app.post("/extract")
async def extract_endpoint(file: UploadFile = File(...)):
    """
    POST multipart/form-data with 'file' (.pdf/.pptx).
    Runs: python extraction.py <tmpfile>
    Returns: {"text": "...cleaned text..."}
    """
    suffix = Path(file.filename).suffix.lower()
    if suffix not in [".pdf", ".pptx"]:
        raise HTTPException(400, "Only .pdf or .pptx accepted")

    with tempfile.TemporaryDirectory() as td:
        td = Path(td)
        in_path = td / f"input{suffix}"
        out_path = td / "output.txt"
        in_path.write_bytes(await file.read())

        # run using absolute paths; CWD = temp dir
        run([PYTHON_EXE, str(EXTRACT), str(in_path)], cwd=td)

        if not out_path.exists():
            raise HTTPException(500, f"extraction.py didn't produce {out_path}")
        cleaned = out_path.read_text(encoding="utf-8", errors="ignore")

    return {"text": cleaned}

@app.post("/summarize")
def summarize_endpoint(req: SummarizeReq):
    """
    Runs: python summarize.py cleaned.txt summarized.txt
    Returns: {"summary": "..."}
    """
    with tempfile.TemporaryDirectory() as td:
        td = Path(td)
        cleaned = td / "cleaned.txt"
        summ = td / "summarized.txt"
        cleaned.write_text(req.text, encoding="utf-8")
        run([PYTHON_EXE, str(SUMMARIZE), str(cleaned), str(summ)], cwd=td)
        if not summ.exists():
            raise HTTPException(500, "summarize.py didn't produce summarized.txt")
        return {"summary": summ.read_text(encoding="utf-8", errors="ignore")}

@app.post("/generate")
def generate_endpoint(req: GenerateReq):
    """
    Runs: python generateQuestions.py summarized.txt questions.txt
    Returns: {"questions":"q1\\nq2\\n...", "count": N}
    """
    with tempfile.TemporaryDirectory() as td:
        td = Path(td)
        summ = td / "summarized.txt"
        qfile = td / "questions.txt"
        summ.write_text(req.summary, encoding="utf-8")
        run([PYTHON_EXE, str(GENERATE), str(summ), str(qfile)], cwd=td)
        if not qfile.exists():
            raise HTTPException(500, "generateQuestions.py didn't produce questions.txt")
        questions = qfile.read_text(encoding="utf-8", errors="ignore").strip()
        lines = [ln for ln in questions.splitlines() if ln.strip()]
        return {"questions": "\n".join(lines), "count": len(lines)}

@app.post("/grade")
def grade_endpoint(req: GradeReq):
    """
    Runs: python grader.py summarized.txt playerAnswers.txt results.json
    Returns: parsed JSON from results.json
    """
    with tempfile.TemporaryDirectory() as td:
        td = Path(td)
        summ = td / "summarized.txt"
        ans = td / "playerAnswers.txt"
        out = td / "results.json"
        summ.write_text(req.summary, encoding="utf-8")
        ans.write_text(req.full_text, encoding="utf-8")
        run([PYTHON_EXE, str(GRADER), str(summ), str(ans), str(out)], cwd=td)
        if not out.exists():
            raise HTTPException(500, "grader.py didn't produce results.json")
        try:
            data = json.loads(out.read_text(encoding="utf-8"))
        except Exception as e:
            raise HTTPException(500, f"results.json not valid JSON: {e}")
        return data

# run: uvicorn server:app --host 0.0.0.0 --port 8000
