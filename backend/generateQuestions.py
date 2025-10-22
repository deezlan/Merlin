import os
import sys
from openai import OpenAI
from dotenv import load_dotenv
load_dotenv()

api_key = os.getenv("OPENAI_KEY")
client = OpenAI(api_key=api_key)
MODEL = "gpt-4o-mini"

SYSTEM_PROMPT = """You create discriminative exam-style questions that test understanding and key recall.
Output 6–10 questions, strictly one per line, newline separated, plain text only.
Rules:
- No numbering, bullets, or blank lines.
- Focus on why/how/compare/apply/correct.
- Include minimal must-memorise items (definitions/formulas/named laws) marked " [MEM]".
- Each question ≤ 160 characters.
- Use only the provided summary; do not invent facts; no duplicates.
"""

def generate_questions(summary_text: str) -> str:
    """Generate 6–10 understanding + recall questions."""
    response = client.chat.completions.create(
        model=MODEL,
        temperature=0.3,
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": f"Summary:\n<<<\n{summary_text}\n<<<\nGenerate the questions now."},
        ],
    )
    # clean up output
    text = response.choices[0].message.content.strip()
    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]
    return "\n".join(lines)

def main(in_file: str, out_file: str):
    with open(in_file, "r", encoding="utf-8") as f:
        summary = f.read()

    print("Generating questions...")
    questions = generate_questions(summary)

    with open(out_file, "w", encoding="utf-8") as f:
        f.write(questions)

    print(f"Generated questions saved to: {out_file}")
    print(f"Count: {len(questions.splitlines())} question(s)")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python generateQuestions.py summary.txt questions.txt")
        sys.exit(1)
    main(sys.argv[1], sys.argv[2])
