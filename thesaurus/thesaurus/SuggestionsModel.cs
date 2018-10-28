﻿using System;
using Dynamo.ViewModels;
using Dynamo.Graph.Nodes;
using System.Reflection;
using Dynamo.Models;
using Accord.Statistics.Models.Markov;
using Accord.Statistics.Filters;
using Accord.IO;
using System.Linq;
using Accord.MachineLearning.Bayes;

namespace thesaurus
{
    public class SuggestionsModel
    {
        private DynamoViewModel _dynamoViewModel { get; set; }

        public DynamoViewModel DynamoViewModel
        {
            get { return _dynamoViewModel; }
            set { _dynamoViewModel = value; }
        }
        private object loadedModel;
        private Codification loadedCodebook;

        public SuggestionsModel(DynamoViewModel dvm)
        {
            DynamoViewModel = dvm;
            LoadModel();
        }

        /// <summary>
        /// This handler responds to clicking on the SuggestionNodeButton and create node to the Dynamo
        /// session current workspace
        /// </summary>
        /// <param name="nodeName"></param>
        /// <returns></returns>
        public bool PlaceNode(string nodeName)
        {
            // Get Reference of DynamoModel
            var dm = DynamoViewModel.Model;
            var nsm = dm.SearchModel;

            foreach (var se in nsm.SearchEntries)
            {
                if (se.FullName.EndsWith(nodeName, StringComparison.OrdinalIgnoreCase) || se.CreationName.EndsWith(nodeName, StringComparison.OrdinalIgnoreCase))
                {
                    var dynMethod = se.GetType().GetMethod("ConstructNewNodeModel",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var obj = dynMethod.Invoke(se, new object[] { });
                    var nM = obj as NodeModel;

                    try
                    {
                        dm.ExecuteCommand(new DynamoModel.CreateNodeCommand(nM, 0, 0, true, false));
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            return false;
        }

        private void LoadModel()
        {
            if (TrainModel.trainingMode == "bayes")
            {
                loadedModel = Serializer.Load<NaiveBayes>("thesaurus_bayes.accord");
                loadedCodebook = Serializer.Load<Codification>("thesaurus_codebook.accord");
            }
            else if (TrainModel.trainingMode == "markov")
            {
                loadedModel = Serializer.Load<HiddenMarkovModel>("thesaurus_HMModel.accord");
                loadedCodebook = Serializer.Load<Codification>("thesaurus_codebook.accord");
            }
        }

        public string[] Predict(string nodeName)
        {
            try
            {
                if (TrainModel.trainingMode == "bayes")
                {
                    NaiveBayes bayesModel = loadedModel as NaiveBayes;
                    int[] instance = loadedCodebook.Transform(nodeName);
                    int c = bayesModel.Decide(instance);
                    string result = loadedCodebook.Revert("output", c);

                    return new string[] { result };
                }
                else if (TrainModel.trainingMode == "markov")
                {
                    HiddenMarkovModel markovModel = loadedModel as HiddenMarkovModel;
                    // Test a hard coded example
                    int code = loadedCodebook.Transform("Nodes", nodeName);
                    int[] predictSample = markovModel.Predict(observations: new[] { code }, next: 1);
                    string[] predictResult = loadedCodebook.Revert("Nodes", predictSample);
                    return predictResult;
                }
                return null;
            }
            catch
            {
                return new string[] { };
            }
        }
    }
}
