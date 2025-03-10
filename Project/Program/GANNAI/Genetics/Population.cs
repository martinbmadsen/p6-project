﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using Utility;

namespace Genetics {
    public class Population {
        private SortList<AIPlayer> individuals;

        public int Generation { get; private set; }

        public Simulation Simulation;

        public Population(Simulation simulation, int generation) {
            Simulation = simulation;
            Generation = generation;
            individuals = new SortList<AIPlayer>();
            InitializeRandomPopulation();
        }

        /// <summary>
        ///Performs an iteration, where new individuals are born by crossover, mutation and crossover-mutation.
        ///A new individual replaces an old individual only if it has a greater fitness.
        /// </summary>
        public void Iterate() {
            List<AIPlayer> offspring = BreedIndividuals();

            CalcFitnessValuesThreaded(offspring);
            Simulation.offspringMerger.Merge(individuals, offspring, Simulation);
            individuals.Crop(Simulation.PopulationSize);
            Generation++;
        }


        /// <summary>
        /// Calculates the fitness value of each individual threaded
        /// </summary>
        /// <param name="offspring"></param>
        private void CalcFitnessValuesThreaded(List<AIPlayer> list) {
            Task[] tasks = new Task[list.Count];
            for (int i = 0; i < list.Count; i++) {
                int index = i;
                AITrainableGame game = Simulation.Game;
                tasks[index] = Task.Factory.StartNew(() => { list[index].CalcFitness(game); });
            }
            Task.WaitAll(tasks);  
        }


        //Returns a list of new individuals bred from the current population
        private List<AIPlayer> BreedIndividuals() {

            int crossovers = (int)(Simulation.PopulationSize * Simulation.CrossoverBredAmount);
            int mutations = Simulation.PopulationSize - crossovers;
            int crossoverMutations = (int)(Simulation.MutateAfterCrossoverAmount * Simulation.MutateAfterCrossoverAmount);
            crossovers -= crossoverMutations;

            List<AIPlayer> newlyBred = new List<AIPlayer>();
            for (int i = 0; i < mutations; i++) {
                AIPlayer parent = SelectIndividualRankBased();
                Chromosome newChromosome = parent.Chromosome.GetMutated(Simulation.MutationRate);
                AIPlayer toAdd = new AIPlayer(newChromosome, parent, null, Simulation.NeuralNetworkMaker);
                newlyBred.Add(toAdd);
            }
            for (int i = 0; i < crossovers; i++) {
                AIPlayer parent1 = SelectIndividualRankBased();
                AIPlayer parent2 = SelectIndividualRankBased();
                CrossoverMethod crossoverMethod = Simulation.RandomCrossoverMethod();
                Chromosome crossedChromosome = crossoverMethod.Cross(parent1.Chromosome, parent2.Chromosome);
                AIPlayer toAdd = new AIPlayer(crossedChromosome, parent1, parent2, Simulation.NeuralNetworkMaker);
                newlyBred.Add(toAdd);
            }
            for (int i = 0; i < crossoverMutations; i++) {
                AIPlayer parent1 = SelectIndividualRankBased();
                AIPlayer parent2 = SelectIndividualRankBased();
                CrossoverMethod crossoverMethod = Simulation.RandomCrossoverMethod();
                Chromosome crossedChromosome = crossoverMethod.Cross(parent1.Chromosome, parent2.Chromosome);
                Chromosome crossedAndMutatedChromosome = crossedChromosome.GetMutated(Simulation.MutationRate);
                AIPlayer toAdd = new AIPlayer(crossedAndMutatedChromosome, parent1, parent2, Simulation.NeuralNetworkMaker);
                newlyBred.Add(toAdd);
            }

            //Parallel.For(0, newlyBred.Count, i => newlyBred[i].CalcFitness(Simulation.Game));
            foreach (AIPlayer aip in newlyBred) {
                aip.CalcFitness(Simulation.Game);
            }
            return newlyBred;
        }
        //A weighted random selection of an individual based on the rank of each individual (least fitness has rank 1, greatest fitness has rank n)
        private AIPlayer SelectIndividualRankBased() {
            RankMethod rankMethod = new LinearRankMethod();
            return individuals.Get(rankMethod.GetRandomIndex(individuals.Count));
        }

        public void InitializeRandomPopulation() {
            double cloneRate = Simulation.InitialSimilarity;
            double mutateRate = Simulation.InitialMutation;
            if (cloneRate > 1.0 || cloneRate < 0.0)
                throw new Exception("clone rate is not set correctly.");
            if (individuals == null)
                individuals = new SortList<AIPlayer>();
            individuals.Clear();

            AIPlayer cloneIndividual = new AIPlayer(Simulation.NeuralNetworkMaker);
            cloneIndividual.CalcFitness(Simulation.Game);
            individuals.Add(cloneIndividual);
            int cloneSize = (int)(cloneRate * Simulation.PopulationSize);
            for (int i = 1; i < cloneSize; i++) {
                Chromosome clonedChromosome = cloneIndividual.Chromosome.Clone();
                if (mutateRate > 0.0)
                    clonedChromosome = clonedChromosome.GetMutated(mutateRate);
                cloneIndividual = new AIPlayer(clonedChromosome, Simulation.NeuralNetworkMaker);
                cloneIndividual.CalcFitness(Simulation.Game);
                individuals.Add(cloneIndividual);
            }

            int randSize = Simulation.PopulationSize - cloneSize;
            for (int i = 0; i < randSize; i++) {
                AIPlayer randomIndividual = new AIPlayer(Simulation.NeuralNetworkMaker);
                randomIndividual.CalcFitness(Simulation.Game);
                individuals.Add(randomIndividual);
            }
        }

        /// <summary>
        /// Returns the most fit individual in the population
        /// </summary>
        /// <returns></returns>
        public AIPlayer GetBest() {
            return individuals.Get(0);
        }

        /// <summary>
        /// Gets the least fit individual.
        /// </summary>
        /// <returns>The least fit individual in the population.</returns>
        public AIPlayer GetWorst() {
            return individuals.Get(individuals.Count - 1);
        }

        /// <summary>
        /// Gets the mean individual.
        /// </summary>
        /// <returns>The individual in the middle of the list of individuals.</returns>
        public AIPlayer GetMean() {
            return individuals.Get((int)(individuals.Count / 2.0));
        }

        /// <summary>
        /// Gets the average fitness of the entire population.
        /// </summary>
        /// <returns>The average fitness of the population.</returns>
        public double GetAverage() {
            double total = 0.0;
            for (int i = 0; i < individuals.Count; i++)
                total += individuals.Get(i).GetFitness();
            return total / individuals.Count;
        }

        /// <summary>
        /// Returns the fitness values of all individuals in descending order
        /// </summary>
        /// <returns></returns>
        public double[] GetFitnessValues() {
            double[] result = new double[individuals.Count];
            for (int i = 0; i < individuals.Count; i++)
                result[i] = individuals.Get(i).GetFitness();
            return result;
        }

        public Dictionary<string, double> GetDiversityMeasurements() {
            Dictionary<string, double> measurementMap = new Dictionary<string, double>();

            foreach(IDiversityMeasure divMeasure in Simulation.diversityMeasures) {
                double diversity = divMeasure.MeasureDiversity(individuals);

                measurementMap.Add(divMeasure.Name, diversity);
            }
            return measurementMap;
        }

        /// <summary>
        /// Measures the diversity.
        /// </summary>
        /// <returns>The diversity.</returns>
        /// <param name="runs">Number of runs.</param>
        public double MeasureDiversity() {
          //Simulation.offspringMerger.Merge(individuals, offspring, Simulation);
          return Simulation.diversityMeasure.MeasureDiversity(individuals);
        }

        public string MeasureDiversityMethod() {
          return Simulation.diversityMeasure.Name;
        }
  }
}
