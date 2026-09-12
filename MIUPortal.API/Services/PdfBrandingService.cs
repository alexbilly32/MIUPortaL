using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;

namespace MIUPortal.API.Services
{
    public static class PdfBrandingService
    {
        public static readonly DeviceRgb MiuRed = new DeviceRgb(193, 39, 45);
        public static readonly DeviceRgb MiuGreen = new DeviceRgb(30, 91, 50);
        public static readonly DeviceRgb MiuBlack = new DeviceRgb(35, 31, 32);
        public static readonly DeviceRgb LightGrey = new DeviceRgb(245, 245, 245);
        public static readonly DeviceRgb MidGrey = new DeviceRgb(225, 225, 225);

        private const string CrestPath = "wwwroot/assets/branding/miu-crest.png";
        private const string WordmarkPath = "wwwroot/assets/branding/miu-wordmark.png";

        public static PdfFont GetHeaderFont() => PdfFontFactory.CreateFont("Helvetica-Bold");
        public static PdfFont GetBodyFont() => PdfFontFactory.CreateFont("Helvetica");

        public static void AddLetterhead(iText.Layout.Document document, string officeSubtitle)
        {
            var headerFont = GetHeaderFont();
            var bodyFont = GetBodyFont();

            if (File.Exists(CrestPath))
            {
                var crestData = ImageDataFactory.Create(CrestPath);
                var crestImg = new Image(crestData)
                    .SetWidth(55)
                    .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                    .SetMarginBottom(4);
                document.Add(crestImg);
            }

            document.Add(new Paragraph("METROPOLITAN INTERNATIONAL UNIVERSITY")
                .SetFont(headerFont)
                .SetFontSize(15)
                .SetFontColor(MiuBlack)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(2));


            Table contactTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 }))
                .UseAllAvailableWidth()
                .SetMarginTop(2)
                .SetMarginBottom(0);

            contactTable.AddCell(NoBorderCell(
                "P. O Box 160, Kisoro, Uganda\nDirect Tel: +256 701650111\nReception: +256 393225927 / +256 772 561957",
                bodyFont, TextAlignment.LEFT));

            contactTable.AddCell(NoBorderCell(
                "Kamonyi, Northern Division, Kisoro\nEmail: admissions@miu.ac.ug\nWebsite: www.miu.ac.ug",
                bodyFont, TextAlignment.RIGHT));

            document.Add(contactTable);

            
            document.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(1f))
                .SetMarginTop(4)
                .SetMarginBottom(8)
                .SetFontColor(MiuBlack));

            if (!string.IsNullOrEmpty(officeSubtitle))
            {
                document.Add(new Paragraph(officeSubtitle)
                    .SetFont(headerFont)
                    .SetFontSize(11)
                    .SetUnderline()
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginBottom(16));
            }
        }

        private static Cell NoBorderCell(string text, PdfFont font, TextAlignment align)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(font).SetFontSize(8))
                .SetBorder(Border.NO_BORDER)
                .SetTextAlignment(align)
                .SetPadding(0);
        }

        public static Paragraph SectionTitle(string text)
        {
            return new Paragraph(text)
                .SetFont(GetHeaderFont())
                .SetFontSize(11)
                .SetFontColor(MiuRed)
                .SetMarginTop(12)
                .SetMarginBottom(6);
        }


        public static void AddLabelValueRow(Table table, string label, string value)
        {
            table.AddCell(new Cell()
                .Add(new Paragraph(label).SetFont(GetHeaderFont()).SetFontSize(10))
                .SetBorder(Border.NO_BORDER)
                .SetPaddingBottom(4));

            table.AddCell(new Cell()
                .Add(new Paragraph($": {value ?? "N/A"}").SetFont(GetBodyFont()).SetFontSize(10))
                .SetBorder(Border.NO_BORDER)
                .SetPaddingBottom(4));
        }


        public static Cell TableHeaderCell(string text)
        {
            return new Cell()
                .Add(new Paragraph(text).SetFont(GetHeaderFont()).SetFontSize(9).SetFontColor(ColorConstants.WHITE))
                .SetBackgroundColor(MiuGreen)
                .SetPadding(5)
                .SetTextAlignment(TextAlignment.CENTER);
        }

        public static Cell TableBodyCell(string text, TextAlignment align = TextAlignment.LEFT)
        {
            return new Cell()
                .Add(new Paragraph(text ?? "").SetFont(GetBodyFont()).SetFontSize(9))
                .SetPadding(5)
                .SetTextAlignment(align);
        }

        public static Div StatusStamp(string label, string subLabel)
        {
            var stampDiv = new Div()
                .SetWidth(110)
                .SetHeight(110)
                .SetBorder(new SolidBorder(MiuRed, 2))
                .SetBorderRadius(new BorderRadius(55))
                .SetHorizontalAlignment(HorizontalAlignment.RIGHT)
                .SetPadding(8)
                .SetMarginTop(10);

            stampDiv.Add(new Paragraph(label)
                .SetFont(GetHeaderFont())
                .SetFontSize(13)
                .SetFontColor(MiuRed)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(2));

            stampDiv.Add(new Paragraph(subLabel)
                .SetFont(GetBodyFont())
                .SetFontSize(6)
                .SetFontColor(MiuRed)
                .SetTextAlignment(TextAlignment.CENTER));

            return stampDiv;
        }

        
        public static IBlockElement StudentPhotoContent(string? photoPath, float maxWidth = 110, float maxHeight = 140)
        {
            if (!string.IsNullOrEmpty(photoPath) && File.Exists(photoPath))
            {
                try
                {
                    
                    var img = new Image(ImageDataFactory.Create(photoPath))
                        .SetMaxWidth(maxWidth)
                        .SetMaxHeight(maxHeight)
                        .SetAutoScale(true);

                    return new Paragraph()
                        .Add(img)
                        .SetTextAlignment(TextAlignment.CENTER)
                        .SetMargin(0);
                }
                catch
                {
                    
                }
            }

            return new Paragraph("Photo\nNot Available")
                .SetFont(GetBodyFont())
                .SetFontSize(8)
                .SetFontColor(MidGrey)
                .SetTextAlignment(TextAlignment.CENTER);
        }

       
        public static Cell StudentPhotoCell(string? photoPath, float maxWidth = 110, float maxHeight = 140)
        {
            return new Cell()
                .SetBorder(new SolidBorder(MidGrey, 1))
                .SetPadding(4)
                .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                .SetVerticalAlignment(VerticalAlignment.MIDDLE)
                .SetTextAlignment(TextAlignment.CENTER)
                .Add(StudentPhotoContent(photoPath, maxWidth, maxHeight));
        }

        public static void AddFooter(iText.Layout.Document document, string officeLine)
        {
            document.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(0.5f))
                .SetMarginTop(16)
                .SetMarginBottom(4)
                .SetFontColor(MidGrey));

            document.Add(new Paragraph(officeLine)
                .SetFont(GetBodyFont())
                .SetFontSize(8)
                .SetFontColor(MiuBlack)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(1));

            document.Add(new Paragraph("P. O Box 160, KISORO, UGANDA  |  www.miu.ac.ug  |  +256 772 561 957")
                .SetFont(GetBodyFont())
                .SetFontSize(7)
                .SetFontColor(MiuBlack)
                .SetTextAlignment(TextAlignment.CENTER));
        }
    }
}