using System;
using System.Collections.Generic;

namespace CodexCue.Tests {
    internal sealed class TestRegistry {
        private readonly IList<TestCase> tests = new List<TestCase>();

        public void Add(string name, Action action) {
            tests.Add(new TestCase(name, action));
        }

        public int Run(string filter) {
            int passed = 0;
            int failed = 0;
            foreach (TestCase test in tests) {
                if (!String.IsNullOrEmpty(filter) && test.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try {
                    test.Action();
                    Console.WriteLine("PASS " + test.Name);
                    passed++;
                } catch (Exception error) {
                    Console.Error.WriteLine("FAIL " + test.Name + ": " + error.Message);
                    failed++;
                }
            }
            Console.WriteLine("RESULT passed=" + passed + " failed=" + failed);
            return failed == 0 ? 0 : 1;
        }

        private sealed class TestCase {
            public TestCase(string name, Action action) { Name = name; Action = action; }
            public string Name { get; private set; }
            public Action Action { get; private set; }
        }
    }

    internal static class Assert {
        public static void Equal<T>(T expected, T actual) {
            if (!Object.Equals(expected, actual)) {
                throw new Exception("Expected <" + expected + "> but was <" + actual + ">.");
            }
        }

        public static void True(bool value) {
            if (!value) throw new Exception("Expected true but was false.");
        }

        public static void False(bool value) {
            if (value) throw new Exception("Expected false but was true.");
        }

        public static T Throws<T>(Action action) where T : Exception {
            try { action(); } catch (T error) { return error; }
            throw new Exception("Expected exception " + typeof(T).Name + ".");
        }

        public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual) {
            IList<T> expectedItems = new List<T>(expected);
            IList<T> actualItems = new List<T>(actual);
            if (expectedItems.Count != actualItems.Count) {
                throw new Exception("Expected sequence count <" + expectedItems.Count + "> but was <" + actualItems.Count + ">.");
            }
            for (int index = 0; index < expectedItems.Count; index++) {
                if (!Object.Equals(expectedItems[index], actualItems[index])) {
                    throw new Exception("Sequences differ at index " + index + ".");
                }
            }
        }
    }
}
