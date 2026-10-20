from pkg.textutil import normalize


class WordCounter:
    def __init__(self):
        self.counts = {}

    def add(self, text):
        for word in self.split(normalize(text)):
            self.counts[word] = self.counts.get(word, 0) + 1

    def split(self, text):
        return text.split()

    def top(self):
        return max(self.counts, key=self.counts.get)


def count_all(lines):
    counter = WordCounter()
    for line in lines:
        counter.add(line)
    return counter.top()
