from core.RAG import FactBase


def build_fact_base(pdf_file, output_file):
    """Read the report, keep its numbered statements and save them as a JSON file."""
    fact_base = FactBase.from_pdf(pdf_file)
    fact_base.save(output_file)
    return fact_base


if __name__ == "__main__":
    pdf_file = "../data/raw/IPCC_AR6_SYR_SPM.pdf"
    output_file = "facts/ipcc_ar6_syr_spm.json"
    fact_base = build_fact_base(pdf_file, output_file)
    print(f"{len(fact_base.statements)} statements saved in {output_file}")
