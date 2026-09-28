"""Check the localization table before shipping or adding a language pack."""

from collections import Counter
from pathlib import Path
import re
import struct


TABLE = Path(__file__).parent / "Assets/Resources/Localization/LocalizationTable.txt"
HEADER = [
    "Key", "Korean", "English", "Japanese", "ChineseSimplified",
    "ChineseTraditional", "SpanishLatinAmerica", "PortugueseBrazil",
    "German", "French", "Indonesian",
]
FORMATS = re.compile(r"\{[^{}]+\}")
TAGS = re.compile(r"<[^>]+>")


def main() -> None:
    lines = TABLE.read_text(encoding="utf-8").splitlines()
    assert lines[0].split("\t") == HEADER, "Unexpected localization columns"
    keys = set()
    count = 0
    for number, line in enumerate(lines[1:], 2):
        if not line or line.startswith("#"):
            continue
        cells = line.split("\t")
        assert 6 <= len(cells) <= len(HEADER), f"Line {number}: column count"
        assert all(cells[:3]), f"Line {number}: key, Korean and English are required"
        assert cells[0] not in keys, f"Line {number}: duplicate key {cells[0]}"
        keys.add(cells[0])
        english = cells[2]
        for column in range(3, len(cells)):
            translation = cells[column]
            if not translation:  # Empty translations intentionally fall back to English.
                continue
            assert translation != english or len(english) <= 80, (
                f"Line {number}, {HEADER[column]}: long English text left untranslated"
            )
            assert Counter(FORMATS.findall(translation)) == Counter(FORMATS.findall(english)), (
                f"Line {number}, {HEADER[column]}: format placeholders"
            )
            assert TAGS.findall(translation) == TAGS.findall(english), (
                f"Line {number}, {HEADER[column]}: TMP tags"
            )
            assert translation.count(r"\n") == english.count(r"\n"), (
                f"Line {number}, {HEADER[column]}: line breaks"
            )
            assert translation.count("[") == english.count("[") and (
                translation.count("]") == english.count("]")
            ), f"Line {number}, {HEADER[column]}: brackets"
            assert len(english) < 30 or len(translation) <= len(english) * 2, (
                f"Line {number}, {HEADER[column]}: suspiciously long translation"
            )
        count += 1
    print(f"{count} localization rows valid")
    check_language_picker(lines)


def check_language_picker(lines: list[str]) -> None:
    expected = ["한국어", "English", "日本語", "简体中文", "繁體中文",
                "Español (Latinoamérica)", "Português (Brasil)", "Deutsch", "Français", "Bahasa Indonesia"]
    rows = [line.split("\t") for line in lines if line.startswith("Language.")]
    names = [row[index + 1] for index, row in enumerate(rows)]
    assert names == expected, f"Language picker must use native names: {names}"

    # Read the font's Unicode cmap (format 12) without requiring a font package.
    # 폰트는 Resources 밖에 있다 (2026-09-16) — Resources 안의 것은 안 쓰여도 빌드에 실린다.
    # 굽는 재료로만 남아 있고, 드롭다운은 미리 구운 LanguagePickerFont 가 그린다.
    font = Path(__file__).parent / "Assets/_project/6.Fonts/Source/NotoSansCJKsc-Regular.otf"
    data = font.read_bytes()
    table_count = struct.unpack_from(">H", data, 4)[0]
    tables = [struct.unpack_from(">4sIII", data, 12 + 16 * i) for i in range(table_count)]
    cmap = next(offset for tag, _, offset, _ in tables if tag == b"cmap")
    encoding_count = struct.unpack_from(">H", data, cmap + 2)[0]
    offsets = [cmap + struct.unpack_from(">HHI", data, cmap + 4 + 8 * i)[2] for i in range(encoding_count)]
    unicode_map = next(offset for offset in offsets if struct.unpack_from(">H", data, offset)[0] == 12)
    group_count = struct.unpack_from(">I", data, unicode_map + 12)[0]
    groups = [struct.unpack_from(">III", data, unicode_map + 16 + 12 * i) for i in range(group_count)]
    missing = {c for c in "".join(names) if not any(start <= ord(c) <= end and glyph + ord(c) - start > 0
               for start, end, glyph in groups)}
    assert not missing, f"Language picker glyphs missing: {missing}"
    print("10 native language names and bundled font glyphs valid")


if __name__ == "__main__":
    main()
