using System;
using System.IO;

var testPath = "test_dir";
Directory.CreateDirectory(testPath);

var id = Guid.NewGuid().ToString();
var path = Path.Combine(testPath, $"{id}.json");
File.WriteAllText(path, "{\"Id\":\"" + id + "\",\"Title\":\"Doc 1\"}");

Console.WriteLine($"Created {id}.json");

// Imagine LoadDocument:
var loadedId = id;
var documentId = loadedId;

// Imagine SaveDocument:
var newDocId = Guid.NewGuid().ToString(); // GetDocument() returns new Document
var newDocPath = Path.Combine(testPath, $"{newDocId}.json");

// Check if DocumentId is empty
if (!string.IsNullOrEmpty(documentId)) {
    newDocId = documentId;
} else {
    documentId = newDocId;
}

var finalPath = Path.Combine(testPath, $"{newDocId}.json");
File.WriteAllText(finalPath, "{\"Id\":\"" + newDocId + "\",\"Title\":\"Doc 1 Updated\"}");

Console.WriteLine($"Saved to {finalPath}");
var files = Directory.GetFiles(testPath, "*.json");
Console.WriteLine($"Total files: {files.Length}");
