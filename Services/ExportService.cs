using WPF = System.Windows.Documents;
using OpenXml = DocumentFormat.OpenXml.Wordprocessing;
using Drawing = DocumentFormat.OpenXml.Drawing;
using BiblicalSearchEngine.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using MaterialDesignThemes.Wpf.Transitions;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;
using A = DocumentFormat.OpenXml.Drawing;

namespace BiblicalSearchEngine.Services
{
    public class ExportService
    {
        public void ExportToWord(Document document, string filePath, ExportOptions options)
        {
            using (var wordDoc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                // Ajouter la partie principale
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = new Body();

                // Titre
                var titlePara = new Paragraph();
                var titleRun = new Run();
                titleRun.Append(new Text(document.Title));
                titleRun.RunProperties = new RunProperties(
                    new Bold(),
                    new FontSize { Val = "32" }
                );
                titlePara.Append(titleRun);
                body.Append(titlePara);

                // Métadonnées
                if (options.IncludeMetadata)
                {
                    var metaPara = new Paragraph();
                    metaPara.Append(new Run(new Text($"Date: {document.DateAdded:dd/MM/yyyy}")));
                    body.Append(metaPara);

                    if (document.Tags.Any())
                    {
                        var tagsPara = new Paragraph();
                        tagsPara.Append(new Run(new Text($"Tags: {string.Join(", ", document.Tags)}")));
                        body.Append(tagsPara);
                    }
                }

                // Espacement
                body.Append(new Paragraph());

                // Contenu avec formatage
                if (options.FormatAsOutline && options.OutlineStructure != null)
                {
                    // Exporter selon la structure
                    foreach (var section in options.OutlineStructure)
                    {
                        // Titre de section
                        var sectionPara = new Paragraph();
                        var sectionRun = new Run(new Text(section.Title));
                        sectionRun.RunProperties = new RunProperties(
                            new Bold(),
                            new FontSize { Val = "24" }
                        );
                        sectionPara.Append(sectionRun);
                        body.Append(sectionPara);

                        // Contenu de section
                        var contentPara = new Paragraph();
                        contentPara.Append(new Run(new Text(section.Content)));
                        body.Append(contentPara);

                        // Espacement
                        body.Append(new Paragraph());
                    }
                }
                else
                {
                    // Export simple du contenu
                    var lines = document.Content.Split('\n');
                    foreach (var line in lines)
                    {
                        var para = new Paragraph();
                        para.Append(new Run(new Text(line)));
                        body.Append(para);
                    }
                }

                // Références bibliques en notes de bas de page
                if (options.IncludeBibleReferences && options.BibleReferences != null)
                {
                    // Ajouter une section de références
                    body.Append(new Paragraph());
                    var refTitle = new Paragraph();
                    var refRun = new Run(new Text("Références Bibliques"));
                    refRun.RunProperties = new RunProperties(new Bold());
                    refTitle.Append(refRun);
                    body.Append(refTitle);

                    foreach (var reference in options.BibleReferences)
                    {
                        var refPara = new Paragraph();
                        refPara.Append(new Run(new Text($"• {reference.Display}: {reference.VerseText}")));
                        body.Append(refPara);
                    }
                }

                mainPart.Document.Append(body);
                mainPart.Document.Save();
            }
        }

        public void ExportToPowerPoint(Document document, string filePath, ExportOptions options)
        {
            using (var pptDoc = PresentationDocument.Create(filePath, PresentationDocumentType.Presentation))
            {
                var presentationPart = pptDoc.AddPresentationPart();
                presentationPart.Presentation = new Presentation();

                // Créer la structure de base
                CreatePresentationParts(presentationPart);

                // Diapositive de titre
                var slidePart1 = CreateSlide(presentationPart);
                AddTitleSlide(slidePart1, document.Title, $"Date: {document.DateAdded:dd/MM/yyyy}");

                // Diapositives de contenu
                if (options.FormatAsOutline && options.OutlineStructure != null)
                {
                    foreach (var section in options.OutlineStructure)
                    {
                        var slidePart = CreateSlide(presentationPart);
                        AddContentSlide(slidePart, section.Title, section.Points);
                    }
                }
                else
                {
                    // Créer des diapositives automatiquement
                    var paragraphs = document.Content.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < paragraphs.Length; i++)
                    {
                        var slidePart = CreateSlide(presentationPart);
                        AddContentSlide(slidePart, $"Partie {i + 1}", new[] { paragraphs[i] });
                    }
                }

                // Diapositive de conclusion avec références
                if (options.IncludeBibleReferences && options.BibleReferences?.Any() == true)
                {
                    var slidePartRef = CreateSlide(presentationPart);
                    var references = options.BibleReferences.Select(r => r.Display).ToArray();
                    AddContentSlide(slidePartRef, "Références Bibliques", references);
                }

                // Sauvegarder
                presentationPart.Presentation.Save();
            }
        }

        private void CreatePresentationParts(PresentationPart presentationPart)
        {
            var slideMasterIdList = new SlideMasterIdList(new SlideMasterId { Id = 2147483648U, RelationshipId = "rId1" });
            var slideIdList = new SlideIdList();
            var slideSize = new SlideSize { Cx = 9144000, Cy = 6858000, Type = SlideSizeValues.Screen4x3 };
            var notesSize = new NotesSize { Cx = 6858000, Cy = 9144000 };
            var defaultTextStyle = new DefaultTextStyle();

            presentationPart.Presentation.Append(slideMasterIdList, slideIdList, slideSize, notesSize, defaultTextStyle);

            // Créer le slide master
            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
            GenerateSlideMasterPart(slideMasterPart);

            // Créer le slide layout
            var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId1");
            GenerateSlideLayoutPart(slideLayoutPart);
        }

        private SlidePart CreateSlide(PresentationPart presentationPart)
        {
            var slidePart = presentationPart.AddNewPart<SlidePart>();
            var slideIdList = presentationPart.Presentation.SlideIdList;

            uint slideId = 256U;
            uint maxSlideId = slideIdList.ChildElements
                .Cast<SlideId>()
                .Select(x => x.Id.Value)
                .DefaultIfEmpty(256U)
                .Max();

            slideId = maxSlideId + 1;

            var slide = new Slide(new CommonSlideData(new ShapeTree()));
            slidePart.Slide = slide;

            var slideId1 = new SlideId { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) };
            slideIdList.Append(slideId1);

            return slidePart;
        }

        private void AddTitleSlide(SlidePart slidePart, string title, string subtitle)
        {
            var shape1 = slidePart.Slide.CommonSlideData.ShapeTree.AppendChild(new Shape());
            shape1.NonVisualShapeProperties = new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = 1U, Name = "Title" },
                new NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties());

            shape1.ShapeProperties = new ShapeProperties();

            shape1.TextBody = new TextBody(
                new BodyProperties(),
                new ListStyle(),
                new Paragraph(new Run(new Text(title))));

            // Sous-titre
            var shape2 = slidePart.Slide.CommonSlideData.ShapeTree.AppendChild(new Shape());
            shape2.NonVisualShapeProperties = new NonVisualShapeProperties(
                new NonVisualDrawingProperties { Id = 2U, Name = "Subtitle" },
                new NonVisualShapeDrawingProperties(),
                new ApplicationNonVisualDrawingProperties());

            shape2.ShapeProperties = new ShapeProperties();

            shape2.TextBody = new TextBody(
                new BodyProperties(),
                new ListStyle(),
                new Paragraph(new Run(new Text(subtitle))));
        }

        private void AddContentSlide(SlidePart slidePart, string title, string[] points)
        {
            // Titre
            var titleShape = slidePart.Slide.CommonSlideData.ShapeTree.AppendChild(new Shape());
            titleShape.TextBody = new TextBody(
                new BodyProperties(),
                new ListStyle(),
                new Paragraph(new Run(new Text(title))));

            // Points
            var contentShape = slidePart.Slide.CommonSlideData.ShapeTree.AppendChild(new Shape());
            var textBody = new TextBody(new BodyProperties(), new ListStyle());

            foreach (var point in points)
            {
                textBody.Append(new Paragraph(new Run(new Text("• " + point))));
            }

            contentShape.TextBody = textBody;
        }

        private void GenerateSlideMasterPart(SlideMasterPart slideMasterPart)
        {
            var slideMaster = new SlideMaster(
                new CommonSlideData(new ShapeTree()),
                new ColorMap());

            slideMasterPart.SlideMaster = slideMaster;
        }

        private void GenerateSlideLayoutPart(SlideLayoutPart slideLayoutPart)
        {
            var slideLayout = new SlideLayout(
                new CommonSlideData(new ShapeTree()),
                new ColorMapOverride());

            slideLayoutPart.SlideLayout = slideLayout;
        }
    }

    public class ExportOptions
    {
        public bool IncludeMetadata { get; set; } = true;
        public bool IncludeBibleReferences { get; set; } = true;
        public bool FormatAsOutline { get; set; } = false;
        public List<OutlineSection> OutlineStructure { get; set; }
        public List<BibleReference> BibleReferences { get; set; }
    }

    public class OutlineSection
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string[] Points { get; set; }
    }
}
