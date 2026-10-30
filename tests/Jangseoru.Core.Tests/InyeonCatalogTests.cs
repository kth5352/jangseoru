using System.Linq;
using Xunit;

namespace Jangseoru.Core.Tests
{
    [Collection("InyeonCatalog")]
    public class InyeonCatalogTests
    {
        [InyeonTheory]
        [InlineData("special811_2", "special811", "/受傷/")]
        [InlineData("special838_2", "special838", "/憔悴/")]
        [InlineData("special841_2", "special841", "/衰毛/")]
        [InlineData("special208_1", "special208", "/section_normal.png")]
        public void Verified_appearance_variants_share_one_identity_and_picture_list(string alias, string person, string path)
        {
            Assert.Equal(person, InyeonCatalog.ResolveStory(alias));
            Assert.Null(InyeonCatalog.Find(alias));
            Assert.Contains(InyeonCatalog.Find(person).Pictures, p => p.Contains(path));
            Assert.NotEqual(InyeonCatalog.ResolveStory("special402"), InyeonCatalog.ResolveStory("special402_3"));
        }

        [InyeonFact]
        public void Costumes_and_childhood_belong_to_the_same_person()
        {
            Assert.Equal("girl7", InyeonCatalog.ResolveStory("girl7_3"));
            Assert.Equal("player", InyeonCatalog.ResolveStory("player_child"));
            Assert.Contains(InyeonCatalog.Find("girl7").Pictures, p => p.Contains("盛裝"));
            Assert.Contains(InyeonCatalog.Find("girl7").Pictures, p => p.Contains("幼年"));
        }

        [InyeonFact]
        public void Similar_ids_do_not_merge_different_people_or_generic_portraits()
        {
            Assert.Equal("girl2", InyeonCatalog.ResolveStory("girl2_5"));
            Assert.Equal("girl2_4", InyeonCatalog.ResolveStory("girl2_3"));
            Assert.NotEqual(InyeonCatalog.ResolveStory("girl2"), InyeonCatalog.ResolveStory("girl2_4"));
            Assert.NotEqual(InyeonCatalog.ResolveStory("beggar1"), InyeonCatalog.ResolveStory("beggar2"));
        }

        [InyeonFact]
        public void Picture_validation_is_per_person_and_catalog_limited()
        {
            const string key = "Assets/__Project/Images/Characters/Master/normal.png";
            Assert.True(InyeonCatalog.IsPicture("master", key));
            Assert.False(InyeonCatalog.IsPicture("girl7", key));
            Assert.False(InyeonCatalog.IsPicture("master", "Mods/external.png"));
            Assert.Null(InyeonCatalog.ResolveStory("unknown_mod_character"));
            Assert.Null(InyeonCatalog.Find(null));
        }

        [InyeonFact]
        public void Relationship_variant_maps_to_person_but_keeps_its_text_id()
        {
            var relation = InyeonCatalog.Relationship(502);
            Assert.Equal("girl5", relation.CharacterId);
            Assert.Equal("girl5_11", relation.TextId);
            Assert.Contains("girl5", InyeonCatalog.Find("girl5").BiographyIds);
            Assert.Equal("icon_head_master", InyeonCatalog.Find("master").HeadIcon);
            Assert.All(InyeonCatalog.Characters, c => Assert.Equal(c.Pictures.Count, c.Pictures.Distinct().Count()));
        }
    }
}
