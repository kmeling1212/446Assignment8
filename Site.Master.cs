using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Text;

namespace _446Assignment8
{
    public partial class SiteMaster : MasterPage
    {
        //Global Variables
        public static ConcurrentBag<KeyValuePair<string, int>> wordBag = new ConcurrentBag<KeyValuePair<string, int>>(); //create wordBag for words
        public BlockingCollection<KeyValuePair<string, int>> wordChunks = new BlockingCollection<KeyValuePair<string, int>>(wordBag); //create wordChunks Blocking Collection
        public ConcurrentDictionary<string, int> wordStore = new ConcurrentDictionary<string, int>(); //create wordstore dictionary

        public IEnumerable<string> NameNode()
        {
            int blockSize = Convert.ToInt32(sizeLabel.Text) / Convert.ToInt32(NBox.Text); //set block size
            int startPos = 0; //set start position
            int len = 0; //set length
            string text = FileName.Text.ToLower(); //set text variable
            for (int i = 0; i < text.Length; i++)
            {
                i = i + blockSize > text.Length - 1 ? text.Length - 1 : i + blockSize; //set i to either text.Length - 1 or i + blockSize
                while (i >= startPos && text[i] != ' ')
                {
                    i--; //backtrack to first space within blockSize
                }

                if (i == startPos)
                {
                    i = i + blockSize > (text.Length - 1) ? text.Length - 1 : i + blockSize; //if back to start, reset i
                    len = (i - startPos) + 1; //set length as i - start position + 1
                } else
                {
                    len = i - startPos; //if not back at start, set length
                }

                yield return text.Substring(startPos, len).Trim(); //yield return substring block
                startPos = i; //update startPos to end of new block
            }
        }

        public void mapWords()
        {
            Parallel.ForEach(NameNode(), wordBlock => //execute in parallel
            {
                string[] strings = wordBlock.Split(',', '.', ';', ':', '?', '!',
                '(', ')', '{', '}', '[', ']', '\\', '/', '\r', '\n', ' '); //split wordBlock
                StringBuilder buffer = new StringBuilder(); //create buffer

                foreach (string sub in strings)
                {
                    foreach (char c in sub)
                    {
                        //Filter out punctuation and spaces (hyphenated words count as one word)
                        if (char.IsLetterOrDigit(c) || c == '-' || c == '\'')
                        {
                            buffer.Append(c);
                        }

                    }
                    //add word to blocking collection
                    if (buffer.Length > 0)
                    {
                        KeyValuePair<string, int> word = new KeyValuePair<string, int>(buffer.ToString(), 1); //create key value pair of word and occurence count
                        wordChunks.Add(word); //add word to wordChunks bag
                        buffer.Clear(); //clear buffer
                    }
                    
                }
            });

            wordChunks.CompleteAdding(); //finished adding words to wordChunks bag
        }

        public void reduceWords()
        {
            Parallel.ForEach(wordChunks.GetConsumingEnumerable(), word =>
            {
                wordStore.AddOrUpdate(word.Key, word.Value, (key, oldValue) => Interlocked.Increment(ref oldValue)); //if word is already in word store, increment value by 1
                //otherwise add the word with default value of 1
            });
        }

        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void UploadButton_Click(object sender, EventArgs e)
        {
            FileName.Text = System.IO.File.ReadAllText(@ChooseFileBox.Text); //read in file
            sizeLabel.Text = FileName.Text.Length.ToString(); //set sizeLabel as file text
            StatusLabel.Text = StatusLabel.Text + ": Complete"; //update status label

        }

        protected void RunMapReduceButton_Click(object sender, EventArgs e)
        {
            var time = new System.Diagnostics.Stopwatch(); //create stopwatch to calculate execution time
            time.Start(); //start timing
            if (wordChunks.IsAddingCompleted) //if already run, reset
            {
                wordBag = new ConcurrentBag<KeyValuePair<string, int>>(); //create new word bag
                wordChunks = new BlockingCollection<KeyValuePair<string, int>>(wordBag); //create new word chunks blocking collection
            }

            System.Threading.ThreadPool.QueueUserWorkItem(delegate (object state)
            {
                mapWords(); //Create background process to map input data
            });

            reduceWords(); //reduce mapped words

            //Print words in Display Results label
            StringBuilder bob = new StringBuilder(); //create string builder
            int totalWords = 0; //set total words counter to 0
            foreach (KeyValuePair<string, int> kvp in wordStore) //iterate through wordStore
            {
                bob.AppendLine(kvp.Key + ": " + kvp.Value); //add key and value to bob
                totalWords += kvp.Value; //increment word counter
            }
            time.Stop(); //stop execution time counter because map reduce is done
            TotalWordsLabel.Text = "Total Words: " + totalWords.ToString(); //display total words
            WordsListLabel.Text = bob.ToString(); //display words
            if (Convert.ToInt32(NBox.Text) == 1) //if single thread
            {
                SingleThreadLabel.Text = SingleThreadLabel.Text + " " + time.ElapsedMilliseconds + " ms"; //display execution time as single thread
            } else
            {
                MultithreadLabel.Text = MultithreadLabel.Text + " " + time.ElapsedMilliseconds + " ms"; //display execution time as multithreading
            }
        }
    }
}