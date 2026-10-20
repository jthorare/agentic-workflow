def normalize(text):
    return strip_marks(text.lower())


def strip_marks(text):
    return "".join(c for c in text if c.isalnum() or c == " ")
