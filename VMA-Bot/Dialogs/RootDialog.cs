using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Threading;

namespace VMA_Bot.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            Activity activity = await result as Activity;
            string text = activity.Text;
            string answer = "";
            //if (text == "你好" || text == "你好啊" || text == "你好呀" || text == "您好" || text == "你好！" || text == "您好！" || text == "嗨" || text == "嗨！" || text == "6" || text == "66" || text == "666" || text == "66666" || text == "666666" || text == "hi" || text == "hello" || text == "Hi" || text == "Hello" || text == "hi!" || text == "Hi!" || text == "hello!" || text == "Hello!" || text == "nihao" || text == "nihao!" || text == "？" || text == "？？" || text == "？？？")
            //    answer = "您好，我是VMA的机器人，我可以为您解答关于深圳市万科梅沙书院的问题 mua!";

            //if (answer == "")
                try
                {
                    answer = Bot.go(text);
                }
                catch (Exception ex)
                {
                    answer = "出现错误，请稍候再试！<br>" + ex.Message;
                }

            await context.PostAsync(answer);

            context.Wait(MessageReceivedAsync);
        }
    }
}