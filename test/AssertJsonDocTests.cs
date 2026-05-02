using System.Text.Json;

namespace OSSMWebServer.Test
{
    public class AssertJsonDocTests
    {
        [Fact]
        public void AssertJsonDoc_Satisfies_ShouldPassForMatchingObjects()
        {
            var expected = new
            {
                Name = "Test",
                Age = 30,
            };
            var actualJson = JsonDocument.Parse(JsonSerializer.Serialize(expected));
            AssertJsonDoc.Satisfies(expected, actualJson.RootElement);
        }

        [Fact]
        public void AssertJsonDoc_Satisfies_ShouldPassWithPartialMatchingObjects()
        {
            var expected = new
            {
                Name = "Test"
            };
            var actualJson = JsonDocument.Parse("{\"Name\":\"Test\",\"Age\":30}");
            AssertJsonDoc.Satisfies(expected, actualJson.RootElement);
        }

        [Fact]
        public void AssertJsonDoc_Satisfies_ShouldThrowForMissingProperty()
        {
            var expected = new
            {
                Name = "Test",
                Age = 30
            };
            var actualJson = JsonDocument.Parse("{\"Name\":\"Test\"}");
            Assert.Throws<Exception>(() => AssertJsonDoc.Satisfies(expected, actualJson.RootElement));
        }

        [Fact]
        public void AssertJsonDoc_Satisfies_WithPredicate_ShouldPassForValidValue()
        {
            var expected = new
            {
                Age = (Func<int, bool>)(age => age > 20)
            };
            var actualJson = JsonDocument.Parse("{\"Age\":25}");
            AssertJsonDoc.Satisfies(expected, actualJson.RootElement);
        }

        [Fact]
        public void AssertJsonDoc_Satisfies_WithPredicate_ShouldThrowForInvalidValue()
        {
            var expected = new
            {
                Age = (Func<int, bool>)(age => age > 20)
            };
            var actualJson = JsonDocument.Parse("{\"Age\":15}");
            Assert.Throws<Exception>(() => AssertJsonDoc.Satisfies(expected, actualJson.RootElement));
        }

        [Fact]
        public void AssertJsonDoc_Satisfies_WithTypeCheck_ShouldPassForValidType()
        {
            var expected = new
            {
                Age = typeof(int)
            };
            var actualJson = JsonDocument.Parse("{\"Age\":25}");
            AssertJsonDoc.Satisfies(expected, actualJson.RootElement);
        }

        [Fact]
        public void AssertJsonDoc_Satisfies_WithTypeCheck_ShouldThrowForInvalidType()
        {
            var expected = new
            {
                Age = typeof(int)
            };
            var actualJson = JsonDocument.Parse("{\"Age\":\"twenty-five\"}");
            Assert.Throws<Exception>(() => AssertJsonDoc.Satisfies(expected, actualJson.RootElement));
        }

        [Fact]
        public void AssertJsonDoc_Satisfies_CaseInsensitive_ShouldPassForMatchingObjects()
        {
            var expected = new
            {
                name = "Test",
                age = 30,
            };
            var actualJson = JsonDocument.Parse("{\"Name\":\"Test\",\"Age\":30}");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            AssertJsonDoc.Satisfies(expected, actualJson.RootElement, options);
        }
    }
}
