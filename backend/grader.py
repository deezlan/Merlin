#!/usr/bin/env python3
import os, sys, json
from openai import OpenAI
from dotenv import load_dotenv
load_dotenv()

MODEL = os.getenv("GEN_MODEL", "gpt-4o-mini")
api_key = os.getenv("OPENAI_KEY")
client = OpenAI(api_key=api_key)

SYSTEM_PROMPT = """You are a strict but fair examiner.
Grade ONLY using the provided summary as reference; do not credit unsupported claims.
Rubric:
- accuracy: 0–10
- completeness: 0–10
- clarity: 0–5
- relevance: 0–5
total = accuracy + completeness + clarity + relevance (0–30)
Rules:
- Be concise and consistent across both students.
- Provide 2-3 sentences of feedback per student.
Return STRICT JSON ONLY in this schema:
{
  "A":{"accuracy":0-10,"completeness":0-10,"clarity":0-5,"relevance":0-5,"total":0-30,"feedback":"The sentences."},
  "B":{"accuracy":0-10,"completeness":0-10,"clarity":0-5,"relevance":0-5,"total":0-30,"feedback":"The sentences."}
}"""

def read_text(path: str) -> str:
    with open(path, "r", encoding="utf-8") as f:
        return f.read()

def score_duel(summary: str, full_text: str) -> dict:
    user_prompt = f"""SUMMARY:
{summary}

TASK:
Read the following text which contains a question and two students' answers labeled as 'Student A' and 'Student B'.
Grade them according to the schema below.

TEXT:
{full_text}

OUTPUT SCHEMA (strict JSON):
{{
  "A": {{"accuracy":0-10,"completeness":0-10,"clarity":0-5,"relevance":0-5,"total":0-30,"feedback":"Two sentences."}},
  "B": {{"accuracy":0-10,"completeness":0-10,"clarity":0-5,"relevance":0-5,"total":0-30,"feedback":"Two sentences."}}
}}"""
    resp = client.chat.completions.create(
        model=MODEL,
        temperature=0.0,
        messages=[
            {"role":"system","content":SYSTEM_PROMPT},
            {"role":"user","content":user_prompt},
        ],
    )
    content = resp.choices[0].message.content.strip()
    return json.loads(content)

def main(summary_path: str, answers_path: str, out_path: str):
    summary = read_text(summary_path)
    full_text = read_text(answers_path)

    # Optional: truncate long summary for cost control
    MAX_SUMMARY_CHARS = 12000
    if len(summary) > MAX_SUMMARY_CHARS:
        summary = summary[:MAX_SUMMARY_CHARS]

    result = score_duel(summary, full_text)

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print(f"Saved scores to {out_path}")
    print(f"A total: {result['A']['total']} | B total: {result['B']['total']}")
    print(f"A feedback: {result['A']['feedback']}")
    print(f"B feedback: {result['B']['feedback']}")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python grader.py summary.txt playerAnswers.txt [output.json]")
        sys.exit(1)
    summary_file = sys.argv[1]
    answers_file = sys.argv[2]
    output_file = sys.argv[3] if len(sys.argv) >= 4 else "results.json"
    main(summary_file, answers_file, output_file)
