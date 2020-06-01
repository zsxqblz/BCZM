using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using MySql.Data.MySqlClient;

namespace VMA_Bot
{
    public class Bot
    {
        //数据库连接字符串
        public static string Conn = "Database='vmabot';Data Source='121.201.101.3';User Id='root';Password='root';Port=51651;charset='utf8';pooling=true";

        public static string[] ID = { null, null };
        public static Hashtable CourseName = new Hashtable();
        public static Question LastQuestion = new Question();
        public static int RecursionCnt = 0;

        public static string go(string QuestionStr)
        {
            if (CourseName.Count == 0)
                InitializeCourseName();
            string answer = null, VProfile = "-1";
            Question question = null;
            bool hololens = false;
            if (QuestionStr.Contains("user:"))
            {
                VProfile = QuestionStr.Split(':')[1];
                QuestionStr = QuestionStr.Split(':')[3];
                hololens = true;
            }
            
            try
            {
                //结构化问题展示
                question = GetQuestionObj(QuestionStr);

                //QnA答案，低于40分输出QnA bad
                answer = QnA.GetAnsFromQnA(QuestionStr);
                if (!(answer == null))
                    goto finish;

                if (hololens)
                {
                    //Hololens的json
                    answer = GetHololensJson(question);
                }
                else
                {
                    //luis答案
                    answer = "";
                    answer = GetAnsFromLUIS(question);
                }
            }
            catch (Exception ex) { answer = "出现错误/r/n" + ex.Message; }

            finish:
            insertQuestionLog(QuestionStr, answer);//加入问题信息到数据库记录中

            return answer;
        }
        public static void InitializeCourseName()
        {
            CourseName.Add("数学", "AP Calculus BC");
            CourseName.Add("AP微积分", "AP Calculus BC");
            CourseName.Add("AP微积分BC", "AP Calculus BC");
            CourseName.Add("AP微积分AB", "AP Calculus AB");
            CourseName.Add("化学", "AP Chemistry");
            CourseName.Add("AP化学", "AP Chemistry");
            CourseName.Add("AP计算机", "AP Computer Science");
            CourseName.Add("英语", "AP English");
            CourseName.Add("AP英语", "AP English");
            CourseName.Add("篮球", "Basketball 3");
            CourseName.Add("生物", "Biology 3");
            CourseName.Add("语文", "Chinese 11");
            CourseName.Add("研讨会", "Civil Science Seminar&nbsp;");
            CourseName.Add("辩论", "Debate");
            CourseName.Add("地理", "Earth &amp; Environmental Science");
            CourseName.Add("武术", "Martial Arts 3");
            CourseName.Add("油画", "Painting");
            CourseName.Add("标化", "Test Prep 3");
            CourseName.Add("AP微观经济", "AP Microeconomics");
            CourseName.Add("微观经济", "AP Microeconomics");
            CourseName.Add("戏剧", "Dance Performance");
            CourseName.Add("荣誉英语", "English 11 Honors");
            CourseName.Add("托福课", "English as a Foreign Language 3");
        }
        //返回利用luis和数据库给出的答案，匹配不到返回null
        public static string GetLUISJson(string Sentense)
        {
            try
            {
                WebRequest wReq = WebRequest.Create("https://westus.api.cognitive.microsoft.com/luis/v2.0/apps/f2fef305-537e-4509-ba34-616f7499e3ed?subscription-key=531bb50b63bd4838bd6302496d42384a&verbose=true&timezoneOffset=0&q=" + Sentense);
                WebResponse wResp = wReq.GetResponse();
                StreamReader sr = new StreamReader(wResp.GetResponseStream(), Encoding.UTF8);
                return sr.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }
        public static Question GetQuestionObj(string QuestionStr)
        {
            Question Question = new Question();
            Question.question = QuestionStr;
            string JsonStr = GetLUISJson(QuestionStr);
            JObject JsonObj = JObject.Parse(JsonStr);
            Question.intent = JsonObj["topScoringIntent"]["intent"].ToString();

            List<JToken> JTokenEntities = JsonObj["entities"].ToList();
            List<Entity> Entities = new List<Entity>();
            foreach (JToken JTokenEntity in JTokenEntities)
            {
                Entity Entity = new Entity();
                Entity.entity = JTokenEntity["entity"].ToString().Replace(" ", "");
                Entity.type = JTokenEntity["type"].ToString();
                Entities.Add(Entity);
            }
            Question.entities = Entities;
            return Question;
        }

        public static string GetAnsFromLUIS(Question Question)
        {
            //调用原来正常的函数拿答案
            string Output = "";
            Output = GetOnlyAnsFromLUIS(Question);
            //初始化全局变量
            Refresh(Question);
            return Output;
        }

        public static string BadRecursion(Question Question)
        {
            Question BadQuestion = GetQuestionObj(LastQuestion.entities[0].entity + Question.question);

            //限定递归一次
            if (RecursionCnt == 0)
            {
                RecursionCnt++;
                return GetAnsFromLUIS(BadQuestion);
            }
            else
            {
                RecursionCnt = 0;
                return null;
            }

        }

        public static string GetOnlyAnsFromLUIS(Question Question)
        {
            string Answer = null;

            //寻找不适用概念
            foreach (Entity e in Question.entities)
                if (e.type == "不适用概念")
                {
                    return "不好意思，" + e.entity + "对万科梅沙书院不适用";
                }

            //开始对于每一种intent进行查询
            if (Question.intent == "上下文")
            {
                bool SameIntent = false;
                foreach (Entity enew in Question.entities)
                    foreach (Entity eold in LastQuestion.entities)
                        if (enew.type == eold.type)
                        {
                            eold.entity = enew.entity;
                            SameIntent = true;
                        }

                //相同intent替换实体就行了
                if (SameIntent)
                    return GetAnsFromLUIS(LastQuestion);
                //不同intent直接把第一个老entity拼在上下文问题前面暴力递归
                return BadRecursion(Question);
            }

            if (Question.intent == "人查询")//intent人查询
            {
                string Name = null;
                string Job = null;
                string Parameter = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "已知人名")
                    {
                        Name = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知职位")
                    {
                        Job = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知参数")
                    {
                        Parameter = e.entity;
                        break;
                    }
                if (Name != null)
                {
                    Answer = getSQL("people2", Name, Parameter);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("people2", Name); //没参数试一下
                    if (Answer != null && Answer != "") //我不知道是返回null还是“”。。。
                        return Answer;
                }
                if (Job != null)
                {
                    Answer = getSQL("job1", Job, Parameter);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("job1", Job); //没参数试一下
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，找不到这个人";
            }

            if (Question.intent == "功能查询")
            {
                string Department = null;
                string Others = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "已知部门")
                    {
                        Department = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "杂实体")
                    {
                        Others = e.entity;
                        break;
                    }
                if (Department != null)
                {
                    Answer = getSQL("department2", Department);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Others != null)
                {
                    Answer = getSQL("other2", Others);
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，这东西不在数据库里";
            }

            if (Question.intent == "地点查询")
            {
                string Location = null;
                string Department = null;
                string Name = null;
                string RoomNo = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "已知地点")
                    {
                        Location = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知部门")
                    {
                        Department = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知人名")
                    {
                        Name = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "门牌号")
                    {
                        RoomNo = e.entity;
                        break;
                    }
                if (Location != null)
                {
                    Answer = getSQL("location1", Location);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Department != null)
                {
                    Answer = getSQL("department1", Department);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Name != null)
                {
                    Answer = getSQL("people1", Name);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (RoomNo != null)
                {
                    return "用hololens看一下吧";
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，找不到这个地点";
            }

            if (Question.intent == "数量查询")
            {
                string Job = null;
                string Other = null;
                string Parameter = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "已知职位")
                    {
                        Job = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "杂实体")
                    {
                        Other = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知参数")
                    {
                        Parameter = e.entity;
                        break;
                    }
                if (Job != null)
                {
                    Answer = getSQL("job2", Job, Parameter);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("job2", Job);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Other != null)
                {
                    Answer = getSQL("other2", Other, Parameter);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("other2", Other);
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，找不到这个东西";
            }

            if (Question.intent == "时间查询")
            {
                string Activity = null;
                string Other = null;
                string Parameter = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "已知活动")
                    {
                        Activity = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "杂实体")
                    {
                        Other = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知参数")
                    {
                        Parameter = e.entity;
                        break;
                    }
                if (Activity != null)
                {
                    Answer = getSQL("activity", Activity, Parameter);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("activity", Activity);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Other != null)
                {
                    Answer = getSQL("other2", Other);
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，查询不到这项活动";
            }

            if (Question.intent == "电话查询")
            {
                string Department = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "已知部门")
                    {
                        Department = e.entity;
                        break;
                    }
                if (Department != null)
                {
                    Answer = getSQL("department3", Department);
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，我不知道这个电话。";
            }

            if (Question.intent == "网站查询")
            {
                if (Question.entities.Count == 0)
                    return "官网是www.vma.edu.cn";
                return "目前只有一个官网，www.vma.edu.cn";

            }

            if (Question.intent == "评论查询")
            {
                string Location = null;
                string Other = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "已知地点")
                    {
                        Location = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "杂实体")
                    {
                        Other = e.entity;
                        break;
                    }
                if (Location != null)
                {
                    Answer = getSQL("location2", Location);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Other != null)
                {
                    Answer = getSQL("other1", Other);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("other2", Other);//other1没有就拿other2凑
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，查不到这个东西";
            }

            if (Question.intent == "邮箱查询")
            {
                string Name = null;
                string Department = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "已知人名")
                    {
                        Name = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知部门")
                    {
                        Department = e.entity;
                        break;
                    }
                if (Name != null)
                {
                    Answer = getSQL("people3", Name);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Department != null)
                {
                    Answer = getSQL("department4", Department);
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，这个邮箱是不公开的。";
            }

            if (Question.intent == "实体查询")
            {
                string Other = null;
                string Name = null;
                string Location = null;
                string Department = null;
                string Job = null;
                string Activity = null;
                string Parameter = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "杂实体")
                    {
                        Other = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知人名")
                    {
                        Name = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知地点")
                    {
                        Location = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知部门")
                    {
                        Department = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知职位")
                    {
                        Job = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知活动")
                    {
                        Activity = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知参数")
                    {
                        Parameter = e.entity;
                        break;
                    }

                if (Other != null)
                {
                    Answer = getSQL("other2", Other);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Name != null)
                {
                    Answer = getSQL("people2", Name);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Location != null)
                {
                    Answer = getSQL("location1", Location);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Department != null)
                {
                    Answer = getSQL("department2", Department);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Job != null)
                {
                    Answer = getSQL("job1", Job, Parameter);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("job1", Job);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("job2", Job, Parameter);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("job2", Job);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Activity != null)
                {
                    Answer = getSQL("activity", Activity, Parameter);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("activity", Activity);
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，查不到这个东西";
            }

            if (Question.intent == "二值查询")
            {
                string Other = null;
                string Name = null;
                string Location = null;
                string Department = null;
                string Job = null;
                string Activity = null;
                string RoomNo = null;
                string Parameter = null;
                int cnt = 0;
                foreach (Entity e in Question.entities)
                    if (e.type == "杂实体")
                    {
                        Other = e.entity;
                        cnt++;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知人名")
                    {
                        Name = e.entity;
                        cnt++;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知地点")
                    {
                        Location = e.entity;
                        cnt++;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知部门")
                    {
                        Department = e.entity;
                        cnt++;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知职位")
                    {
                        Job = e.entity;
                        cnt++;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知活动")
                    {
                        Activity = e.entity;
                        cnt++;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "门牌号")
                    {
                        RoomNo = e.entity;
                        cnt++;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知参数")
                    {
                        Parameter = e.entity;
                        break;
                    }

                //只有一个实体，确认存在
                if (cnt == 1)
                {
                    if (Other != null)
                    {
                        Answer = getSQL("yesno", Other);
                        if (Answer != null && Answer != "")
                            return Answer;
                    }
                    if (Name != null)
                    {
                        Answer = getSQL("yesno", Name);
                        if (Answer != null && Answer != "")
                            return Answer;
                    }
                    if (Location != null)
                    {
                        Answer = getSQL("yesno", Location);
                        if (Answer != null && Answer != "")
                            return Answer;
                    }
                    if (Department != null)
                    {
                        Answer = getSQL("yesno", Department);
                        if (Answer != null && Answer != "")
                            return Answer;
                    }
                    if (Job != null)
                    {
                        Answer = getSQL("yesno", Job, Parameter);
                        if (Answer != null && Answer != "")
                            return Answer;
                    }
                    if (Activity != null)
                    {
                        Answer = getSQL("yesno", Activity);
                        if (Answer != null && Answer != "")
                            return Answer;
                    }
                    return "没有";
                }


                //实体，参数和答案对搜索有没有
                int check = -1;
                if (Name != null && RoomNo != null)
                {
                    check = CheckSQL("people1", Name, RoomNo, Parameter);
                    if (check == 1)
                        return "是";
                    else if (check == 0)
                        return "不是";
                }

                if (Name != null && Job != null)
                {
                    check = CheckSQL("people2", Name, Job, Parameter);
                    if (check == 1)
                        return "是";
                    else if (check == 0)
                        return "不是";
                }

                if (Department != null && RoomNo != null)
                {
                    check = CheckSQL("department1", Department, RoomNo, Parameter);
                    if (check == 1)
                        return "是";
                    else if (check == 0)
                        return "不是";
                }

                if (Location != null && RoomNo != null)
                {
                    check = CheckSQL("location1", Location, RoomNo, Parameter);
                    if (check == 1)
                        return "是";
                    else if (check == 0)
                        return "不是";
                }
            
                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "这世界太复杂，我也说不清是非，看不透存在";
            }

            if (Question.intent == "简介查询")
            {
                string Other = null;
                string Name = null;
                string Location = null;
                string Department = null;
                string Job = null;
                string Activity = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "杂实体")
                    {
                        Other = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知人名")
                    {
                        Name = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知地点")
                    {
                        Location = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知部门")
                    {
                        Department = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知职位")
                    {
                        Job = e.entity;
                        break;
                    }
                foreach (Entity e in Question.entities)
                    if (e.type == "已知活动")
                    {
                        Activity = e.entity;
                        break;
                    }

                if (Other != null)
                {
                    Answer = getSQL("other1", Other);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("other2", Other);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Name != null)
                {
                    Answer = getSQL("people2", Name);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Location != null)
                {
                    Answer = getSQL("location2", Location);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Department != null)
                {
                    Answer = getSQL("department2", Department);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Job != null)
                {
                    Answer = getSQL("job1", Job);
                    if (Answer != null && Answer != "")
                        return Answer;
                    Answer = getSQL("job2", Job);
                    if (Answer != null && Answer != "")
                        return Answer;
                }
                if (Activity != null)
                {
                    Answer = getSQL("activity", Activity);
                    if (Answer != null && Answer != "")
                        return Answer;
                }

                string BadRecAns = BadRecursion(Question);
                if (BadRecAns != null)
                    return BadRecAns;
                return "不好意思，查不到这项介绍";
            }

            if (Question.intent == "None")
                return "抱歉，本Bot还没聪明到能回答这个问题";


            LastQuestion = Question;
            return "抱歉，本Bot还没聪明到能回答这个问题";
        }

        public static void Refresh(Question Question)
        {
            LastQuestion = Question;
            RecursionCnt = 0;
        }

        public static string getSQL(string table, string entity, string parameter = null)
        {
            try
            {
                string cmdText = parameter == null ? ("select * from " + table + " where entity=N'" + entity + "'") : ("select * from " + table + " where entity=N'" + entity + "' and parameter=N'" + parameter + "'");
                MySqlCommand commn = new MySqlCommand(cmdText, new MySqlConnection(Conn));
                MySqlDataReader sdr = commn.ExecuteReader();
                sdr.Read();
                string answer = sdr.GetString("answer");
                return answer;
            }
            catch { return null; }
        }

        public static int CheckSQL(string table, string entity, string answer, string parameter = null)
        {
            int output = 2;
            try
            {
                string cmdText = parameter == null ? ("select * from " + table + " where entity=N'" + entity + "' and answer=N'" + answer + "'") : ("select * from " + table + " where entity=N'" + entity + "' and parameter=N'" + parameter + "' and answer=N'" + answer + "'");
                MySqlCommand commn = new MySqlCommand(cmdText, new MySqlConnection(Conn));
                MySqlDataReader sdr = commn.ExecuteReader();
                sdr.Read();
                if (sdr.HasRows)
                    output = 1;
                else
                    output = 0;
                return output;
            }
            catch { return 2; }
        }

        public static string[] getSQLID(string VoiceID) //手动滑稽
        {
            string[] str = { "51516", "QGQDH" };
            return str;
        }
        public static string GetHololensJson(Question Question)
        {
            Hololens Hololens = new Hololens();
            if (Question.intent == "成绩查询")
            {
                Hololens.type = 1;
                if (ID[0] == null || ID[1] == null)
                    ID = getSQLID("不存在的声纹"); //声纹不存在的
                                             //string PWRJsonStr = GetPWRJson(); //从服务器get太慢了，下面直接给了用来调试
                string PWRJsonStr = "{\"term\":\"16-17 - VMA\",\"semester\":\"S2\",\"GPA\":\"1\",\"courses\":[{\"name\":\"AP Calculus BC\",\"grade\":\"A+\",\"gradeValue\":1.0,\"value\":-1,\"hours\":5.0},{\"name\":\"AP Chemistry\",\"grade\":\"A+\",\"gradeValue\":1.0,\"value\":-1,\"hours\":5.0},{\"name\":\"AP Computer Science\",\"grade\":\"A+\",\"gradeValue\":1.0,\"value\":-1,\"hours\":5.0},{\"name\":\"AP English\",\"grade\":\"A-\",\"gradeValue\":1.0,\"value\":-1,\"hours\":5.0},{\"name\":\"Basketball 3\",\"grade\":\"B\",\"gradeValue\":1.0,\"value\":-1,\"hours\":1.0},{\"name\":\"Biology 3\",\"grade\":\"A\",\"gradeValue\":1.0,\"value\":-1,\"hours\":1.0},{\"name\":\"Chinese 11\",\"grade\":\"A-\",\"gradeValue\":1.0,\"value\":-1,\"hours\":1.0},{\"name\":\"Civil Science Seminar&nbsp;\",\"grade\":\"A+\",\"gradeValue\":1.0,\"value\":-1,\"hours\":2.0},{\"name\":\"Debate\",\"grade\":\"A\",\"gradeValue\":1.0,\"value\":-1,\"hours\":2.0},{\"name\":\"Earth &amp; Environmental Science\",\"grade\":\"A-\",\"gradeValue\":1.0,\"value\":-1,\"hours\":2.0},{\"name\":\"Han House Time\",\"grade\":\"\",\"gradeValue\":1.0,\"value\":-1,\"hours\":0.0},{\"name\":\"Martial Arts 3\",\"grade\":\"A+\",\"gradeValue\":1.0,\"value\":-1,\"hours\":1.0},{\"name\":\"Painting\",\"grade\":\"A\",\"gradeValue\":1.0,\"value\":-1,\"hours\":1.0},{\"name\":\"Test Prep 3\",\"grade\":\"A\",\"gradeValue\":1.0,\"value\":-1,\"hours\":0.0},{\"name\":\"UCO Course\",\"grade\":\"\",\"gradeValue\":1.0,\"value\":-1,\"hours\":0.0}]}";
                Grades PWRJsonObj = JsonConvert.DeserializeObject<Grades>(PWRJsonStr);
                Hololens.grades = PWRJsonObj;


                List<string> Courses = new List<string>();
                foreach (Entity e in Question.entities)
                    if (e.type == "已知学科")
                        Courses.Add(e.entity);
                //查总体成绩
                if (Courses.Count == 0)
                {
                    Hololens.response = "这是你的成绩";
                }
                else
                {
                    foreach (string course in Courses)
                    {
                        Course[] CoursesJsonList = PWRJsonObj.courses;
                        string CourseEng = (string)CourseName[course];
                        foreach (Course JT in CoursesJsonList)
                        {
                            if (JT.name == CourseEng)
                            {
                                Hololens.response += "你的" + course + "的成绩是" + JT.grade + "; \n";
                                //Hololens.data += JT.ToString().Replace("\n", "").Replace("  "," ").Replace("{ ", "{") + " "; //这里是截取的
                                break;
                            }
                        }
                    }
                    Hololens.response = Hololens.response.Substring(0, Hololens.response.Count() - 3); //删掉后面多补的
                    Hololens.data = Hololens.data.Substring(0, Hololens.data.Count() - 1); //删掉后面多补的
                }
                return JsonConvert.SerializeObject(Hololens);
            }

            else if (Question.intent == "地点查询")
            {
                string RoomNo = null;
                foreach (Entity e in Question.entities)
                    if (e.type == "门牌号")
                    {
                        RoomNo = e.entity;
                        break;
                    }
                if (RoomNo == null)
                    return null;
                Hololens.type = 0;
                Hololens.response = RoomNo + "在这里：";
                Hololens.data = RoomNo;
                return JsonConvert.SerializeObject(Hololens);
            }

            else
            {
                string response = "";
                try
                {
                    response = QnA.GetAnsFromQnA(Question.question);
                    if (response != null && response != "")
                        Hololens.response = response;
                    else
                    {
                        response = GetAnsFromLUIS(Question);
                        Hololens.response = response;
                    }
                }
                catch { Hololens.response = response; }
            }
            return JsonConvert.SerializeObject(Hololens);
        }
        public static string GetPWRJson()
        {
            try
            {
                WebRequest wReq = WebRequest.Create("http://vma.eastasia.cloudapp.azure.com/?user=" + ID[0] + "&pass=" + ID[1]);
                WebResponse wResp = wReq.GetResponse();
                StreamReader sr = new StreamReader(wResp.GetResponseStream(), Encoding.UTF8);
                return sr.ReadToEnd();
            }
            catch
            {
                return null;
            }
        }

        public static void insertQuestionLog(string q, string answer)
        {
            try
            {
                MySqlCommand commn = new MySqlCommand("insert into questionlog (question, answer) values (\"" + q + "\", \"" + answer + "\");", new MySqlConnection(Conn));
                commn.ExecuteNonQuery();
            }
            catch { }
        }
    }
    public class Question
    {
        public string question { get; set; }
        public string intent { get; set; }
        public List<Entity> entities { get; set; }

        public override string ToString()
        {
            string output = string.Format("[Question: question={0}, intent={1}, entities=[", question, intent);
            foreach (Entity e in entities)
                output += e.ToString();
            output += "]]";
            return output;
        }
    }
    public class Entity
    {
        public string entity { get; set; }
        public string type { get; set; }

        public override string ToString()
        {
            return string.Format("[Entity: entity={0}, type={1}]", entity, type);
        }
    }
    public class Hololens
    {
        public int type { get; set; }
        public string response { get; set; }
        public string data { get; set; }
        public Grades grades { get; set; }
    }
    public class Grades
    {
        public string term { get; set; }
        public string semester { get; set; }
        public string GPA { get; set; }
        public Course[] courses { get; set; }
    }
    public class Course
    {
        public string name { get; set; }
        public string grade { get; set; }
        public float gradeValue { get; set; }//能获得的分数，AP、荣誉、普通是不同的，根据等第来算
        public int value { get; set; }
        public float hours { get; set; }
    }

    class QnA
    {
        public static string GetAnsFromQnA(string QuestionStr)//从QnA Maker获取答案
        {
            string JsonStr = GetQnAJson(QuestionStr);
            JObject JsonObj = JObject.Parse(JsonStr);
            string Answer = JsonObj["answers"][0]["answer"].ToString();
            if (double.Parse(JsonObj["answers"][0]["score"].ToString()) > 40)
                if (double.Parse(JsonObj["answers"][0]["score"].ToString()) > 40)
                    return Answer;
            return null;
        }
        public static string GetQnAJson(string Sentense)
        {
            byte[] data = Encoding.UTF8.GetBytes("{\"question\":\"" + Sentense + "\"}");
            WebRequest myRequest = WebRequest.Create("https://westus.api.cognitive.microsoft.com/qnamaker/v2.0/knowledgebases/da58b5fa-1c28-4195-aec5-3b709c5e0432/generateAnswer");
            myRequest.Method = "POST";
            myRequest.Headers.Add("Ocp-Apim-Subscription-Key", "3d32ab501d7d467e91b6a1a2c34274f8");
            myRequest.ContentType = "application/json";
            myRequest.ContentLength = data.Length;
            Stream newStream = myRequest.GetRequestStream();
            newStream.Write(data, 0, data.Length);
            newStream.Close();
            HttpWebResponse myResponse = (HttpWebResponse)myRequest.GetResponse();
            StreamReader reader = new StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
            string result = reader.ReadToEnd();
            reader.Close();
            return result;
        }
    }
}