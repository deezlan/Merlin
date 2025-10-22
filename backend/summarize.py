import openai
import os
import sys
from dotenv import load_dotenv
load_dotenv()

from openai import OpenAI

api_key = os.getenv("OPENAI_KEY")
client = OpenAI(api_key=api_key)
MODEL = "gpt-4o-mini"  # good balance of cost & quality

SYSTEM_PROMPT = """You are a concise academic summarizer.
Your job is to compress long lecture notes into key study notes.
Rules:
- In the first line, specify the current topic at hand in the right context.
- Keep only essential definitions, laws, formulas, named algorithms, steps, and key contrasts.
- No filler or examples unless crucial.
- Output plain text.
- Use one bullet (-) per line, no numbering, no blank lines.
- Keep total length to about 10% of original.
"""

def summarize_text(text: str) -> str:
    resp = client.chat.completions.create(
        model=MODEL,
        temperature=0.2,
        messages=[
            {"role": "system", "content": SYSTEM_PROMPT},
            {"role": "user", "content": f"Summarize the following lecture text:\n\n{text}"}
        ],
    )
    return resp.choices[0].message.content.strip()

def main(in_file: str, out_file: str = "summary.txt"):
    with open(in_file, "r", encoding="utf-8") as f:
        text = f.read()

    print("Summarizing... this may take a few seconds.")
    summary = summarize_text(text)

    with open(out_file, "w", encoding="utf-8") as f:
        f.write(summary)

    print(f"Summary saved to: {out_file}")
    print(f"Approx length ratio: {len(summary)/len(text):.2%}")

if __name__ == "__main__":
    import sys
    if len(sys.argv) < 2:
        print("Usage: python summarize.py cleaned.txt [summary.txt]")
    else:
        infile = sys.argv[1]
        outfile = sys.argv[2] if len(sys.argv) > 2 else "summary.txt"
        main(infile, outfile)