import zipfile
import re
import sys

docx_path = r"C:\Users\omara\OneDrive\Omar Akon Personal\Proyectos OAG\Bufins Web\Common\Nueva web bufins informativa - 2025\Textos nuevos Bufins - Resumido nuevo.docx"

with zipfile.ZipFile(docx_path, 'r') as zip_ref:
    xml_content = zip_ref.read('word/document.xml').decode('utf-8')

# Extract text between <w:t> tags
text_pattern = re.compile(r'<w:t[^>]*>(.*?)</w:t>')
texts = text_pattern.findall(xml_content)
full_text = ''.join(texts)

# Write to file with UTF-8 encoding
with open('clean_content.txt', 'w', encoding='utf-8') as f:
    f.write(full_text)
