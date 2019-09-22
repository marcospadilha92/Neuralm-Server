﻿using AutoMapper;
using Neuralm.Services.Common.Application.Abstractions;
using Neuralm.Services.Common.Application.Interfaces;
using Neuralm.Services.TrainingRoomService.Application.Dtos;
using Neuralm.Services.TrainingRoomService.Application.Interfaces;
using Neuralm.Services.TrainingRoomService.Domain;

namespace Neuralm.Services.TrainingRoomService.Application.Services
{
    /// <summary>
    /// Represents the <see cref="TrainingSessionService"/> class.
    /// </summary>
    public class TrainingSessionService : BaseService<TrainingSession, TrainingSessionDto>, ITrainingSessionService
    {
        /// <summary>
        /// Initializes an instance of the <see cref="TrainingSessionService"/> class.
        /// </summary>
        /// <param name="trainingSessionRepository"></param>
        /// <param name="mapper"></param>
        public TrainingSessionService(
            IRepository<TrainingSession> trainingSessionRepository,
            IMapper mapper) : base(trainingSessionRepository, mapper)
        {

        }

        /*
        /// <inheritdoc cref="ITrainingRoomService.StartTrainingSessionAsync(StartTrainingSessionRequest)"/>
        public async Task<StartTrainingSessionResponse> StartTrainingSessionAsync(StartTrainingSessionRequest startTrainingSessionRequest)
        {
            TrainingRoom trainingRoom;
            Expression<Func<TrainingRoom, bool>> predicate = tr => tr.Id.Equals(startTrainingSessionRequest.TrainingRoomId);
            if ((trainingRoom = await _trainingRoomRepository.FindSingleOrDefaultAsync(predicate)) == default)
                return new StartTrainingSessionResponse(startTrainingSessionRequest.Id, null, "Training room does not exist.");
            if (!await _userRepository.ExistsAsync(usr => usr.Id.Equals(startTrainingSessionRequest.UserId)))
                return new StartTrainingSessionResponse(startTrainingSessionRequest.Id, null, "User does not exist.");

            if (!trainingRoom.IsUserAuthorized(startTrainingSessionRequest.UserId))
                return new StartTrainingSessionResponse(startTrainingSessionRequest.Id, null, "User is not authorized");
            if (!trainingRoom.StartTrainingSession(startTrainingSessionRequest.UserId, out TrainingSession trainingSession))
                return new StartTrainingSessionResponse(startTrainingSessionRequest.Id, null, "Failed to start a training session.");

            await _trainingRoomRepository.UpdateAsync(trainingRoom);

            TrainingSessionDto trainingSessionDto = EntityToDtoConverter.Convert<TrainingSessionDto, TrainingSession>(trainingSession);
            return new StartTrainingSessionResponse(startTrainingSessionRequest.Id, trainingSessionDto, "Successfully started a training session.", true);
        }

        /// <inheritdoc cref="ITrainingRoomService.EndTrainingSessionAsync(EndTrainingSessionRequest)"/>
        public async Task<EndTrainingSessionResponse> EndTrainingSessionAsync(EndTrainingSessionRequest endTrainingSessionRequest)
        {
            TrainingSession trainingSession;
            if ((trainingSession = await _trainingSessionRepository.FindSingleOrDefaultAsync(trs => trs.Id.Equals(endTrainingSessionRequest.TrainingSessionId))) == default)
                return new EndTrainingSessionResponse(endTrainingSessionRequest.Id, "Training session does not exist.");

            if (trainingSession.EndedTimestamp != default)
                return new EndTrainingSessionResponse(endTrainingSessionRequest.Id, "Training session was already ended.");

            trainingSession.EndTrainingSession();
            await _trainingSessionRepository.UpdateAsync(trainingSession);
            return new EndTrainingSessionResponse(endTrainingSessionRequest.Id, "Successfully ended the training session.", true);
        }

        /// <inheritdoc cref="ITrainingRoomService.GetOrganismsAsync(GetOrganismsRequest)"/>
        public async Task<GetOrganismsResponse> GetOrganismsAsync(GetOrganismsRequest getOrganismsRequest)
        {
            string message = "Successfully fetched all requested organisms.";
            TrainingSession trainingSession;
            if (getOrganismsRequest.TrainingSessionId.Equals(Guid.Empty))
                return new GetOrganismsResponse(getOrganismsRequest.Id, new List<OrganismDto>(), "Training room id cannot be an empty guid.");
            if (getOrganismsRequest.Amount < 1)
                return new GetOrganismsResponse(getOrganismsRequest.Id, new List<OrganismDto>(), "Amount cannot be smaller than 1.");
            if ((trainingSession = await _trainingSessionRepository.FindSingleOrDefaultAsync(ts => ts.Id.Equals(getOrganismsRequest.TrainingSessionId))) == default)
                return new GetOrganismsResponse(getOrganismsRequest.Id, new List<OrganismDto>(), "Training session does not exist.");

            // if the list is empty then get new ones from the training room
            if (trainingSession.LeasedOrganisms.Count(o => !o.Organism.Evaluated) == 0)
            {
                if (trainingSession.TrainingRoom.Generation == 0)
                {
                    TrainingRoomSettings trainingRoomSettings = trainingSession.TrainingRoom.TrainingRoomSettings;
                    for (int i = 0; i < trainingRoomSettings.OrganismCount; i++)
                    {
                        Organism organism = new Organism(trainingSession.TrainingRoom.Generation, trainingRoomSettings) { Leased = true };
                        trainingSession.TrainingRoom.AddOrganism(organism);
                        trainingSession.LeasedOrganisms.Add(new LeasedOrganism(organism));
                    }
                    trainingSession.TrainingRoom.IncreaseNodeIdTo(trainingRoomSettings.InputCount + trainingRoomSettings.OutputCount);
                    message = $"First generation; generated {trainingSession.TrainingRoom.TrainingRoomSettings.OrganismCount} organisms.";
                }
                else
                {
                    trainingSession.LeasedOrganisms.AddRange(GetNewLeasedOrganisms(getOrganismsRequest.Amount));
                    message = "Start of new generation.";
                }
            }
            else if (trainingSession.LeasedOrganisms.Count(o => !o.Organism.Evaluated) < getOrganismsRequest.Amount)
            {
                int take = getOrganismsRequest.Amount - trainingSession.LeasedOrganisms.Count(o => !o.Organism.Evaluated);
                List<LeasedOrganism> newLeasedOrganisms = GetNewLeasedOrganisms(take);
                trainingSession.LeasedOrganisms.AddRange(newLeasedOrganisms);
            }

            if (trainingSession.LeasedOrganisms.Count(o => !o.Organism.Evaluated) < getOrganismsRequest.Amount)
                message = "The requested amount of organisms are not all available. The training room is probably close to a new generation or is waiting on other training sessions to complete.";

            await _trainingSessionRepository.UpdateAsync(trainingSession);

            List<OrganismDto> organismDtos = trainingSession.LeasedOrganisms
                .Where(lo => !lo.Organism.Evaluated)
                .Take(getOrganismsRequest.Amount)
                .Select(lo =>
                {
                    OrganismDto organismDto = EntityToDtoConverter.Convert<OrganismDto, Organism>(lo.Organism);
                    // Because the input and output nodes are set using a Many To Many relation the nodes are converted separately.
                    organismDto.InputNodes = lo.Organism.Inputs.Select(input => EntityToDtoConverter.Convert<NodeDto, InputNode>(input.InputNode)).ToList();
                    organismDto.OutputNodes = lo.Organism.Outputs.Select(input => EntityToDtoConverter.Convert<NodeDto, OutputNode>(input.OutputNode)).ToList();
                    return organismDto;
                }).ToList();

            return new GetOrganismsResponse(getOrganismsRequest.Id, organismDtos, message, organismDtos.Any());

            List<LeasedOrganism> GetNewLeasedOrganisms(int take)
            {
                return trainingSession.TrainingRoom.Species.SelectMany(sp => sp.Organisms).Where(lo => !lo.Leased)
                    .Take(take).Select(o =>
                    {
                        o.Leased = true;
                        return new LeasedOrganism(o);
                    }).ToList();
            }
        }

        /// <inheritdoc cref="ITrainingRoomService.PostOrganismsScoreAsync(PostOrganismsScoreRequest)"/>
        public async Task<PostOrganismsScoreResponse> PostOrganismsScoreAsync(PostOrganismsScoreRequest postOrganismsScoreRequest)
        {
            TrainingSession trainingSession = await _trainingSessionRepository.FindSingleOrDefaultAsync(p => p.Id.Equals(postOrganismsScoreRequest.TrainingSessionId));
            if (trainingSession == default)
                return new PostOrganismsScoreResponse(postOrganismsScoreRequest.Id, "Training session does not exist.");
            int count = 0;

            List<LeasedOrganism> orgs = postOrganismsScoreRequest.OrganismScores
                .Select(o =>
                {
                    LeasedOrganism oo = trainingSession.LeasedOrganisms.SingleOrDefault(a => a.OrganismId.Equals(o.Key));
                    if (oo == default)
                        count++;
                    return oo;
                }).ToList();

            if (count > 0)
                return new PostOrganismsScoreResponse(postOrganismsScoreRequest.Id, $"{count} of the organisms does not exist in the training session.");

            foreach (LeasedOrganism leasedOrganism in orgs)
            {
                trainingSession.TrainingRoom.PostScore(leasedOrganism.Organism, postOrganismsScoreRequest.OrganismScores[leasedOrganism.OrganismId]);
            }

            string message = "Successfully updated the organisms scores.";
            if (trainingSession.TrainingRoom.Species.SelectMany(p => p.Organisms).All(lo => lo.Evaluated))
            {
                message = trainingSession.TrainingRoom.EndGeneration()
                    ? "Successfully updated the organisms and advanced a generation!"
                    : "Successfully updated the organisms but failed to advance a generation!";
                trainingSession.LeasedOrganisms.Clear();
            }
            await _trainingSessionRepository.UpdateAsync(trainingSession);
            return new PostOrganismsScoreResponse(postOrganismsScoreRequest.Id, message, true);
        }
        */
    }
}
