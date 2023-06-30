using Microsoft.AspNetCore.Mvc.Formatters;

namespace Juice.AspNetCore.Mvc.Formatters
{
    public class TextSingleValueFormatter : InputFormatter
    {
        private const string TextPlain = "text/plain";
        public TextSingleValueFormatter()
        {
            SupportedMediaTypes.Add(TextPlain);
        }
        public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
        {
            try
            {
                using (var reader = new StreamReader(context.HttpContext.Request.Body))
                {
                    string textSingleValue = await reader.ReadToEndAsync();
                    //Convert from string to target model type (this is the parameter type in the action method)
                    object model = Convert.ChangeType(textSingleValue, context.ModelType);
                    return InputFormatterResult.Success(model);
                }
            }
            catch (Exception ex)
            {
                context.ModelState.TryAddModelError("BodyTextValue", $"{ex.Message} ModelType={context.ModelType}");
                return InputFormatterResult.Failure();
            }
        }

        protected override bool CanReadType(Type type)
        {
            //Put whatever types you want to handle. 
            return type == typeof(string) ||
                type == typeof(int) ||
                type == typeof(DateTime);
        }
        public override bool CanRead(InputFormatterContext context)
        {
            return context.HttpContext.Request.ContentType == TextPlain;
        }
    }
}
