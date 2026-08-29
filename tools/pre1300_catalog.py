# Liber Terra pre-1300 backlog: Gutenberg ID -> plain-text filename.
#
# Curation record only; nothing here is downloaded or built. tools/mvp_works.py
# holds the subset that actually ships, and every ID there is drawn from this
# list. Kept so the catalog can be expanded without redoing the research.
#
# Separation rule:
#   - Group by language/culture of the *original composition*, not the translator.
#   - Prefer English PG texts (translations or facing editions).
#   - Include works by authors who died <= 1300, plus anonymous medieval
#     texts securely dated before 1300 (Beowulf, Roland, early ME, etc.).
#   - Exclude compositions after 1300 (Chaucer, Malory, Sir Gawain ~1360,
#     Froissart, Dante's Comedy, etc.) even if "medieval".
#   - Modern dictionaries/anthologies that *contain* pre-1300 material go
#     under REFERENCE.
#
# IDs verified against https://www.gutenberg.org/cache/epub/feeds/pg_catalog.csv.gz

PRE1300_BOOKS: dict[int, str] = {
    # --- OLD ENGLISH & ANGLO-SAXON (to c.1100) ---
    618: "Codex_Junius_11_Caedmon_manuscript.txt",  # Codex Junius 11
    657: "The_Anglo_Saxon_Chronicle.txt",  # The Anglo-Saxon Chronicle
    981: "Beowulf_Modern_English_Gummere.txt",  # Beowulf
    9701: "Beowulf_and_Fight_at_Finnsburh_OE_text.txt",  # I. Beowulf: an Anglo-Saxon poem. II. The fight at Finnsburh:
    15879: "Elene_Judith_Athelstan_Byrhtnoth_Garnett.txt",  # Elene; Judith; Athelstan, or the Fight at Brunanburh; Byrhtn
    16328: "Beowulf_Anglo_Saxon_Epic_Hall.txt",  # Beowulf: An Anglo-Saxon Epic Poem
    20431: "Beowulf_Morris_Wyatt.txt",  # The Tale of Beowulf, Sometime King of the Folk of the Weder 
    31172: "Old_English_Poems_Selected_Translations.txt",  # Old English Poems Translated into the Original Meter Togethe
    37848: "Old_English_Chronicles_Giles.txt",  # Old English Chronicles
    38326: "Bedes_Ecclesiastical_History_of_England.txt",  # Bede's Ecclesiastical History of England

    # --- EARLY MIDDLE ENGLISH (c.1100–1300) ---
    14305: "Layamons_Brut.txt",  # Layamon's Brut
    26413: "Selections_from_Early_Middle_English_1130_1250_Part1_Texts.txt",  # Selections from Early Middle English, 1130-1250. Part 1: Tex
    32049: "The_Lay_of_Havelok_the_Dane.txt",  # The Lay of Havelok the Dane
    42713: "King_Horn_Floriz_and_Blauncheflur.txt",  # King Horn, Floriz and Blauncheflur, The Assumption of Our La

    # --- OLD FRENCH / ANGLO-NORMAN (pre-1300) ---
    391: "The_Song_of_Roland.txt",  # The Song of Roland
    831: "Four_Arthurian_Romances_Chretien_de_Troyes.txt",  # Four Arthurian Romances
    1578: "Aucassin_and_Nicolete_Lang.txt",  # Aucassin and Nicolete
    2414: "Cliges_Chretien_de_Troyes.txt",  # Cliges: A Romance
    5988: "Old_French_Romances_Morris.txt",  # Old French Romances, Done into English
    10472: "Arthurian_Chronicles_Roman_de_Brut_Wace.txt",  # Arthurian Chronicles: Roman de Brut
    11417: "Lays_of_Marie_de_France.txt",  # French Mediaeval Romances from the Lays of Marie de France
    23227: "Aucassin_and_Nicolette_Bourdillon.txt",  # Aucassin and Nicolette translated from the Old French
    23819: "La_Chanson_de_Roland_Rabillon.txt",  # La Chanson de Roland : Translated from the Seventh Edition o
    38110: "Aucassin_and_Other_Medieval_Romances_Mason.txt",  # Aucassin & Nicolette, and Other Mediæval Romances and Legend
    41163: "Master_Wace_Roman_de_Rou_Norman_Conquest.txt",  # Master Wace, His Chronicle of the Norman Conquest From the R
    46234: "Four_Lais_Marie_de_France_Weston.txt",  # Guingamor, Lanval, Tyolet, Bisclaveret: Four lais rendered i

    # --- MIDDLE HIGH GERMAN (pre-1300) ---
    1151: "The_Nibelungenlied_Shumway.txt",  # The Nibelungenlied
    7321: "The_Nibelungenlied_Needler.txt",  # The Nibelungenlied Translated into Rhymed English Verse in t
    38468: "The_Nibelungenlied_Lettsom.txt",  # The Nibelungenlied Revised Edition

    # --- OLD NORSE / ICELANDIC (written down by 1300) ---
    347: "Saga_of_Grettir_the_Strong.txt",  # The Saga of Grettir the Strong: Grettir's Saga
    597: "Story_of_Burnt_Njal.txt",  # The Story of Burnt Njal: The Great Icelandic Tribune, Jurist
    598: "Heimskringla_Chronicle_of_Kings_of_Norway.txt",  # Heimskringla; Or, The Chronicle of the Kings of Norway
    1152: "Volsunga_Saga_with_Poetic_Edda_excerpts.txt",  # The Story of the Volsungs (Volsunga Saga); with Excerpts fro
    12747: "Story_of_Grettir_the_Strong_Morris.txt",  # The Story of Grettir the Strong
    17803: "Laxdaela_Saga.txt",  # Laxdæla Saga Translated from the Icelandic
    17919: "Burnt_Njal_Dasent.txt",  # The story of Burnt Njal: From the Icelandic of the Njals Sag
    17946: "Eirik_the_Reds_Saga.txt",  # Eirik the Red's Saga
    18947: "The_Younger_Edda_Also_called_Snorres_Edda_or_The_Prose_Edda.txt",  # The Younger Edda; Also called Snorre's Edda, or The Prose Ed
    22093: "The_sagas_of_Olaf_Tryggvason_and_of_Harald_the_Tyrant_Harald_Haardraad.txt",  # The sagas of Olaf Tryggvason and of Harald the Tyrant (Haral
    24420: "Story_of_Frithiof_the_Bold.txt",  # The Story of Frithiof the Bold 1875
    24421: "Story_of_Gunnlaug_the_Worm_Tongue.txt",  # The Story of Gunnlaug the Worm-Tongue and Raven the Skald 18

    # --- WELSH / BRITTONIC ---
    1972: "Historia_Brittonum_Nennius.txt",  # History of the Britons (Historia Brittonum)
    5160: "The_Mabinogion_Guest.txt",  # The Mabinogion

    # --- IBERIAN (pre-1300 tradition) ---
    8491: "Chronicle_of_the_Cid.txt",  # Chronicle of the Cid

    # --- MEDIEVAL & PATRISTIC LATIN (to 1300) ---
    749: "Barlaam_and_Ioasaph.txt",  # Barlaam and Ioasaph
    1092: "The_Description_of_Wales.txt",  # The Description of Wales
    1148: "The_Itinerary_of_Archbishop_Baldwin_Through_Wales.txt",  # The Itinerary of Archbishop Baldwin Through Wales
    3296: "The_Confessions_of_St_Augustine.txt",  # The Confessions of St. Augustine
    6032: "Villehardouin_Chronicle_Fourth_Crusade.txt",  # Memoirs or Chronicle of the Fourth Crusade and the Conquest 
    6493: "Mediaeval_Lore_from_Bartholomew_Anglicus.txt",  # Mediaeval Lore from Bartholomew Anglicus
    13316: "Boethius_Theological_Tractates_and_Consolation.txt",  # The Theological Tractates and The Consolation of Philosophy
    14268: "Historia_Calamitatum.txt",  # Historia Calamitatum
    14328: "Boethius_Consolation_of_Philosophy.txt",  # The Consolation of Philosophy
    14809: "Jordanes_Origin_and_Deeds_of_the_Goths.txt",  # The Origin and Deeds of the Goths
    14981: "Itinerary_of_Benjamin_of_Tudela.txt",  # The Itinerary of Benjamin of Tudela
    17611: "Aquinas_Summa_Theologica_Part_I.txt",  # Summa Theologica, Part I (Prima Pars) From the Complete Amer
    17897: "Aquinas_Summa_Theologica_Part_I_II.txt",  # Summa Theologica, Part I-II (Pars Prima Secundae) From the C
    18590: "The_Letters_of_Cassiodorus_Being_a_Condensed_Translation_of_the_Variae.txt",  # The Letters of Cassiodorus Being a Condensed Translation of 
    18755: "Aquinas_Summa_Theologica_Part_II_II.txt",  # Summa Theologica, Part II-II (Secunda Secundae) Translated b
    19950: "Aquinas_Summa_Theologica_Part_III.txt",  # Summa Theologica, Part III (Tertia Pars) From the Complete A
    22295: "On_prayer_and_the_contemplative_life.txt",  # On prayer and the contemplative life
    35977: "Letters_of_Abelard_and_Heloise_To_which_is_prefixd_a_particular_accoun.txt",  # Letters of Abelard and Heloise To which is prefix'd a partic
    37780: "Chronicle_of_Jocelin_of_Brakelond.txt",  # The Chronicle of Jocelin of Brakelond: A Picture of Monastic
    40227: "The_love_letters_of_Abelard_and_Heloise.txt",  # The love letters of Abelard and Heloise
    40341: "King_Alfreds_Old_English_Version_of_St_Augustines_Soliloquies_Turned_i.txt",  # King Alfred's Old English Version of St. Augustine's Soliloq
    42083: "Chaucers_Translation_of_Boethiuss_De_Consolatione_Philosophiae.txt",  # Chaucer's Translation of Boethius's "De Consolatione Philoso
    45304: "The_City_of_God_Volume_I.txt",  # The City of God, Volume I
    45305: "The_City_of_God_Volume_II.txt",  # The City of God, Volume II
    45843: "Writings_in_Connection_with_the_Donatist_Controversy.txt",  # Writings in Connection with the Donatist Controversy
    49917: "St_John_Damascene_on_Holy_Images_πρὸς_τοὺς_διαβάλλοντας_τᾶς_ἁγίας_εἰκό.txt",  # St John Damascene on Holy Images (πρὸς τοὺς διαβάλλοντας τᾶς
    50524: "Letter_of_Petrus_Peregrinus_on_the_Magnet_1269.txt",  # The Letter of Petrus Peregrinus on the Magnet, A.D. 1269
    58655: "Tales_from_the_Gesta_Romanorum.txt",  # Tales from the Gesta Romanorum
    70561: "The_writings_of_Origen_Vol_1_of_2.txt",  # The writings of Origen, Vol. 1 (of 2)
    70693: "The_writings_of_Origen_Vol_2_of_2.txt",  # The writings of Origen, Vol. 2 (of 2)
    74955: "History_of_the_Franks.txt",  # History of the Franks
    77585: "Confessions_of_St_Augustine.txt",  # Confessions of St. Augustine

    # --- CLASSICAL GREEK ---
    21: "Three_hundred_Aesops_fables_Translated_by_George_Fyler_Townsend.txt",  # Three hundred Aesop’s fables Translated by George Fyler Town
    28: "The_Fables_of_Aesop_Selected_Told_Anew_and_Their_History_Traced.txt",  # The Fables of Aesop Selected, Told Anew, and Their History T
    31: "Plays_of_Sophocles.txt",  # Plays of Sophocles: Oedipus the King; Oedipus at Colonus; An
    150: "The_Republic_pg150.txt",  # The Republic
    806: "Philoktetes.txt",  # Philoktetes
    1169: "Agesilaus.txt",  # Agesilaus
    1170: "Anabasis.txt",  # Anabasis
    1171: "The_Apology.txt",  # The Apology
    1172: "The_Cavalry_General.txt",  # The Cavalry General
    1173: "The_Economist.txt",  # The Economist
    1174: "Hellenica.txt",  # Hellenica
    1175: "Hiero.txt",  # Hiero
    1176: "On_Horsemanship.txt",  # On Horsemanship
    1177: "The_Memorabilia.txt",  # The Memorabilia
    1178: "The_Polity_of_the_Athenians_and_the_Lacedaemonians.txt",  # The Polity of the Athenians and the Lacedaemonians
    1179: "On_Revenues.txt",  # On Revenues
    1180: "The_Sportsman.txt",  # The Sportsman: On Hunting, a Sportsman's Manual, Commonly Ca
    1181: "The_Symposium.txt",  # The Symposium
    1497: "The_Republic_pg1497.txt",  # The Republic
    1571: "Critias.txt",  # Critias
    1572: "Timaeus.txt",  # Timaeus
    1579: "Lysis.txt",  # Lysis
    1580: "Charmides.txt",  # Charmides
    1584: "Laches.txt",  # Laches
    1591: "Protagoras.txt",  # Protagoras
    1598: "Euthydemus.txt",  # Euthydemus
    1600: "Symposium.txt",  # Symposium
    1616: "Cratylus.txt",  # Cratylus
    1635: "Ion.txt",  # Ion
    1636: "Phaedrus.txt",  # Phaedrus
    1642: "Euthyphro.txt",  # Euthyphro
    1643: "Meno.txt",  # Meno
    1656: "Apology.txt",  # Apology
    1657: "Crito.txt",  # Crito
    1658: "Phaedo.txt",  # Phaedo
    1672: "Gorgias.txt",  # Gorgias
    1673: "Lesser_Hippias.txt",  # Lesser Hippias
    1676: "Alcibiades_I.txt",  # Alcibiades I
    1677: "Alcibiades_II.txt",  # Alcibiades II
    1681: "Eryxias.txt",  # Eryxias
    1682: "Menexenus.txt",  # Menexenus
    1687: "Parmenides.txt",  # Parmenides
    1726: "Theaetetus.txt",  # Theaetetus
    1727: "The_Odyssey_Rendered_into_English_prose_for_the_use_of_those_who_canno.txt",  # The Odyssey Rendered into English prose for the use of those
    1728: "The_Odyssey_of_Homer_pg1728.txt",  # The Odyssey of Homer
    1735: "Sophist.txt",  # Sophist
    1738: "Statesman.txt",  # Statesman
    1744: "Philebus.txt",  # Philebus
    1750: "Laws.txt",  # Laws
    1974: "The_Poetics_of_Aristotle.txt",  # The Poetics of Aristotle
    2085: "Cyropaedia.txt",  # Cyropaedia: The Education of Cyrus
    2131: "An_Account_of_Egypt.txt",  # An Account of Egypt
    2199: "The_Iliad_pg2199.txt",  # The Iliad
    2412: "The_Categories.txt",  # The Categories
    2456: "The_History_of_Herodotus_Volume_2.txt",  # The History of Herodotus — Volume 2
    2562: "The_Clouds.txt",  # The Clouds
    2571: "Peace.txt",  # Peace
    2680: "Meditations.txt",  # Meditations
    2707: "The_History_of_Herodotus_Volume_1.txt",  # The History of Herodotus — Volume 1
    3012: "The_Acharnians.txt",  # The Acharnians
    3013: "The_Birds.txt",  # The Birds
    3059: "The_Iliad_pg3059.txt",  # The Iliad
    3160: "The_Odyssey.txt",  # The Odyssey
    4775: "Theocritus_Bion_and_Moschus_Rendered_into_English_Prose.txt",  # Theocritus, Bion and Moschus, Rendered into English Prose
    5063: "The_Iphigenia_in_Tauris_of_Euripides.txt",  # The Iphigenia in Tauris of Euripides
    6130: "The_Iliad_pg6130.txt",  # The Iliad
    6150: "The_Iliad_pg6150.txt",  # The Iliad
    6327: "The_Works_of_Lucian_of_Samosata_Volume_01.txt",  # The Works of Lucian of Samosata — Volume 01
    6585: "The_Works_of_Lucian_of_Samosata_Volume_02.txt",  # The Works of Lucian of Samosata — Volume 02
    6762: "Politics.txt",  # Politics: A Treatise on Government
    6763: "Aristotle_on_the_art_of_poetry.txt",  # Aristotle on the art of poetry
    6829: "The_Works_of_Lucian_of_Samosata_Volume_03.txt",  # The Works of Lucian of Samosata — Volume 03
    6878: "The_Olynthiacs_and_the_Phillippics_of_Demosthenes_Literally_translated.txt",  # The Olynthiacs and the Phillippics of Demosthenes Literally 
    6920: "Thoughts_of_Marcus_Aurelius.txt",  # Thoughts of Marcus Aurelius
    6969: "The_Orations_of_Lysias.txt",  # The Orations of Lysias
    7073: "Specimens_of_Greek_Tragedy_Aeschylus_and_Sophocles.txt",  # Specimens of Greek Tragedy — Aeschylus and Sophocles
    7142: "The_History_of_the_Peloponnesian_War.txt",  # The History of the Peloponnesian War
    7700: "Lysistrata.txt",  # Lysistrata
    7998: "The_Frogs.txt",  # The Frogs
    8418: "Hippolytus_The_Bacchae.txt",  # Hippolytus; The Bacchae
    8438: "The_Nicomachean_ethics_of_Aristotle.txt",  # The Nicomachean ethics of Aristotle
    8604: "The_House_of_Atreus_Being_the_Agamemnon_the_Libation_bearers_and_the_F.txt",  # The House of Atreus; Being the Agamemnon, the Libation beare
    8688: "The_Eleven_Comedies_Volume_1.txt",  # The Eleven Comedies, Volume 1
    8689: "The_Eleven_Comedies_Volume_2.txt",  # The Eleven Comedies, Volume 2
    8714: "Four_Plays_of_Aeschylus.txt",  # Four Plays of Aeschylus
    9060: "The_Public_Orations_of_Demosthenes_volume_1.txt",  # The Public Orations of Demosthenes, volume 1
    9061: "The_Public_Orations_of_Demosthenes_volume_2.txt",  # The Public Orations of Demosthenes, volume 2
    9074: "Stories_from_Thucydides.txt",  # Stories from Thucydides
    10096: "The_Trojan_women_of_Euripides.txt",  # The Trojan women of Euripides
    10162: "Dios_Rome_Volume_3_An_Historical_Narrative_Originally_Composed_in_Gree.txt",  # Dio's Rome, Volume 3 An Historical Narrative Originally Comp
    10430: "Trips_to_the_Moon.txt",  # Trips to the Moon
    10523: "Alcestis.txt",  # Alcestis
    10717: "The_Extant_Odes_of_Pindar_Translated_with_Introduction_and_Short_Notes.txt",  # The Extant Odes of Pindar Translated with Introduction and S
    10883: "Dios_Rome_Volume_4_An_Historical_Narrative_Originally_Composed_in_Gree.txt",  # Dio's Rome, Volume 4 An Historical Narrative Originally Comp
    10890: "Dios_Rome_Volume_5_An_Historical_Narrative_Originally_Composed_in_Gree.txt",  # Dio's Rome, Volume 5 An Historical Narrative Originally Comp
    11339: "Aesops_Fables_a_new_translation.txt",  # Aesop's Fables; a new translation
    11533: "Theocritus_translated_into_English_Verse.txt",  # Theocritus, translated into English Verse
    11607: "Dios_Rome_Volume_2_An_Historical_Narrative_Originally_Composed_in_Gree.txt",  # Dio's Rome, Volume 2 An Historical Narrative Originally Comp
    12061: "Dios_Rome_Volume_6_An_Historical_Narrative_Originally_Composed_in_Gree.txt",  # Dio's Rome, Volume 6 An Historical Narrative Originally Comp
    13726: "Apology_Crito_and_Phaedo_of_Socrates.txt",  # Apology, Crito, and Phaedo of Socrates
    14322: "The_Electra_of_Euripides_Translated_into_English_rhyming_verse.txt",  # The Electra of Euripides Translated into English rhyming ver
    14417: "The_Agamemnon_of_Aeschylus_Translated_into_English_Rhyming_Verse_with.txt",  # The Agamemnon of Aeschylus Translated into English Rhyming V
    14484: "The_Seven_Plays_in_English_Verse.txt",  # The Seven Plays in English Verse
    15081: "The_Tragedies_of_Euripides_Volume_I.txt",  # The Tragedies of Euripides, Volume I.
    15877: "Thoughts_of_Marcus_Aurelius_Antoninus.txt",  # Thoughts of Marcus Aurelius Antoninus
    16452: "The_Iliad_of_Homer_Translated_into_English_Blank_Verse_by_William_Cowp.txt",  # The Iliad of Homer Translated into English Blank Verse by Wi
    17490: "The_Memorable_Thoughts_of_Socrates.txt",  # The Memorable Thoughts of Socrates
    18047: "Dios_Rome_Volume_1_An_Historical_Narrative_Originally_Composed_in_Gree.txt",  # Dio's Rome, Volume 1 An Historical Narrative Originally Comp
    18732: "Aesops_Fables.txt",  # Aesop's Fables: A New Revised Version From Original Sources
    22003: "The_First_Four_Books_of_Xenophons_Anabasis.txt",  # The First Four Books of Xenophon's Anabasis
    22382: "The_Iliad_pg22382.txt",  # The Iliad
    24269: "The_Odyssey_of_Homer_pg24269.txt",  # The Odyssey of Homer
    24856: "Odysseus_the_Hero_of_Ithaca_Adapted_from_the_Third_Book_of_the_Primary.txt",  # Odysseus, the Hero of Ithaca Adapted from the Third Book of 
    26095: "The_Athenian_Constitution.txt",  # The Athenian Constitution
    27458: "Aeschylus_Prometheus_Bound_and_the_Seven_Against_Thebes.txt",  # Aeschylus' Prometheus Bound and the Seven Against Thebes
    27673: "Oedipus_King_of_Thebes_Translated_into_English_Rhyming_Verse_with_Expl.txt",  # Oedipus King of Thebes Translated into English Rhyming Verse
    29510: "An_Essay_on_the_Beautiful_from_the_Greek_of_Plotinus.txt",  # An Essay on the Beautiful, from the Greek of Plotinus
    34588: "Some_of_Æsops_Fables_with_Modern_Instances.txt",  # Some of Æsop's Fables with Modern Instances
    35170: "The_Rhesus_of_Euripides.txt",  # The Rhesus of Euripides
    35171: "The_Trojan_Women_of_Euripides.txt",  # The Trojan Women of Euripides
    35173: "The_Bacchae_of_Euripides.txt",  # The Bacchae of Euripides
    35451: "Medea_of_Euripides.txt",  # Medea of Euripides
    42930: "Plotinos_pg42930.txt",  # Plotinos: Complete Works, v. 1 In Chronological Order, Group
    42931: "Plotinos_pg42931.txt",  # Plotinos: Complete Works, v. 2 In Chronological Order, Group
    42932: "Plotinos_pg42932.txt",  # Plotinos: Complete Works, v. 3 In Chronological Order, Group
    42933: "Plotinos_pg42933.txt",  # Plotinos: Complete Works, v. 4 In Chronological Order, Group
    45858: "Lucians_True_History.txt",  # Lucian's True History
    47242: "The_Works_of_Lucian_of_Samosata_Volume_04.txt",  # The Works of Lucian of Samosata — Volume 04
    48895: "The_Odysseys_of_Homer_together_with_the_shorter_poems.txt",  # The Odysseys of Homer, together with the shorter poems
    51355: "The_Iliads_of_Homer_Translated_according_to_the_Greek.txt",  # The Iliads of Homer Translated according to the Greek
    53103: "Æsops_Fables.txt",  # Æsop's Fables
    53174: "Æschylos_Tragedies_and_Fragments.txt",  # Æschylos Tragedies and Fragments
    55201: "The_Republic_of_Plato.txt",  # The Republic of Plato
    55317: "The_Meditations_of_the_Emperor_Marcus_Aurelius_Antoninus_A_new_renderi.txt",  # The Meditations of the Emperor Marcus Aurelius Antoninus A n
    59058: "Aristotles_History_of_Animals_In_Ten_Books.txt",  # Aristotle's History of Animals In Ten Books
    59225: "The_Lyrical_Dramas_of_Aeschylus_Translated_into_English_Verse.txt",  # The Lyrical Dramas of Aeschylus Translated into English Vers
    60004: "The_Fables_of_Æsop_and_Others_With_Designs_on_Wood.txt",  # The Fables of Æsop, and Others With Designs on Wood
    60874: "Bewicks_Select_Fables_of_Æsop_and_others_In_three_parts_1_Fables_extra.txt",  # Bewick's Select Fables of Æsop and others. In three parts. 1
    68680: "Pausanias_description_of_Greece_Volume_II.txt",  # Pausanias' description of Greece, Volume II.
    68946: "Pausanias_description_of_Greece_Volume_I.txt",  # Pausanias' description of Greece, Volume I.
    72583: "The_genuine_works_of_Hippocrates_Vol_1_of_2.txt",  # The genuine works of Hippocrates, Vol. 1 (of 2)
    76464: "The_dialogues_of_Plato_in_five_volumes_Vol_2_of_5.txt",  # The dialogues of Plato in five volumes, Vol. 2 (of 5)
    77336: "Hecuba_and_other_plays.txt",  # Hecuba and other plays
    77548: "The_Philoctetes_of_Sophocles.txt",  # The Philoctetes of Sophocles
    78618: "The_works_of_Plato_Vol_1_of_6.txt",  # The works of Plato (Vol. 1 of 6)

    # --- CLASSICAL LATIN ---
    228: "The_Aeneid.txt",  # The Aeneid
    230: "The_Bucolics_and_Eclogues.txt",  # The Bucolics and Eclogues
    232: "The_Georgics.txt",  # The Georgics
    785: "On_the_Nature_of_Things.txt",  # On the Nature of Things
    2808: "Treatises_on_Friendship_and_Old_Age.txt",  # Treatises on Friendship and Old Age
    2812: "Letters_of_Marcus_Tullius_Cicero.txt",  # Letters of Marcus Tullius Cicero
    5419: "The_Satires_Epistles_and_Art_of_Poetry_of_Horace.txt",  # The Satires, Epistles, and Art of Poetry of Horace
    5432: "The_Odes_and_Carmen_Saeculare_of_Horace.txt",  # The Odes and Carmen Saeculare of Horace
    7282: "The_Captivi_and_the_Mostellaria.txt",  # The Captivi and the Mostellaria
    7402: "C_Sallusti_Crispi_De_Bello_Catilinario_Et_Jugurthino.txt",  # C. Sallusti Crispi De Bello Catilinario Et Jugurthino
    7491: "De_Amicitia_Scipios_Dream.txt",  # De Amicitia, Scipio's Dream
    7990: "Conspiracy_of_Catiline_and_the_Jurgurthine_War.txt",  # Conspiracy of Catiline and the Jurgurthine War
    9175: "The_Art_of_Poetry.txt",  # The Art of Poetry: an Epistle to the Pisos Q. Horatii Flacci
    9776: "Ciceros_Brutus_or_History_of_Famous_Orators_also_His_Orator_or_Accompl.txt",  # Cicero's Brutus or History of Famous Orators; also His Orato
    10657: "De_Bello_Gallico_and_Other_Commentaries.txt",  # "De Bello Gallico" and Other Commentaries
    11080: "The_Orations_of_Marcus_Tullius_Cicero_Volume_4.txt",  # The Orations of Marcus Tullius Cicero, Volume 4
    13885: "Echoes_from_the_Sabine_Farm.txt",  # Echoes from the Sabine Farm
    14020: "The_Works_of_Horace.txt",  # The Works of Horace
    14945: "Cato_Maior_de_Senectute_with_Introduction_and_Notes.txt",  # Cato Maior de Senectute with Introduction and Notes
    14970: "Academica.txt",  # Academica
    14988: "Ciceros_Tusculan_Disputations_Also_Treatises_On_The_Nature_Of_The_Gods.txt",  # Cicero's Tusculan Disputations Also, Treatises On The Nature
    16564: "Amphitryo_Asinaria_Aulularia_Bacchides_Captivi_Amphitryon_The_Comedy_o.txt",  # Amphitryo, Asinaria, Aulularia, Bacchides, Captivi Amphitryo
    18466: "The_Æneid_of_Virgil_Translated_into_English_Verse.txt",  # The Æneid of Virgil, Translated into English Verse
    18867: "The_Poems_and_Fragments_of_Catullus_Translated_in_the_Metres_of_the_Or.txt",  # The Poems and Fragments of Catullus Translated in the Metres
    20144: "The_Fourth_Book_of_Virgils_Aeneid_and_the_Ninth_Book_of_Voltaires_Henr.txt",  # The Fourth Book of Virgil's Aeneid and the Ninth Book of Vol
    20732: "The_Carmina_of_Caius_Valerius_Catullus.txt",  # The Carmina of Caius Valerius Catullus
    21200: "The_Letters_of_Cicero_Volume_1_The_Whole_Extant_Correspodence_in_Chron.txt",  # The Letters of Cicero, Volume 1 The Whole Extant Correspoden
    22456: "The_Aeneid_of_Virgil_pg22456.txt",  # The Aeneid of Virgil
    28587: "The_Roman_History_of_Ammianus_Marcellinus_During_the_Reigns_of_the_Emp.txt",  # The Roman History of Ammianus Marcellinus During the Reigns 
    29247: "The_Academic_Questions_Treatise_De_Finibus_and_Tusculan_Disputations_o.txt",  # The Academic Questions, Treatise De Finibus, and Tusculan Di
    29358: "The_Æneids_of_Virgil_Done_into_English_Verse.txt",  # The Æneids of Virgil, Done into English Verse
    39355: "Speeches_against_Catilina.txt",  # Speeches against Catilina
    47001: "De_Officiis.txt",  # De Officiis
    57493: "The_Natural_History_of_Pliny_Volume_1.txt",  # Pliny Natural History vol. 1 (Book II = heavens)
    76392: "Physical_science_in_the_time_of_Nero.txt",  # Seneca Naturales Quaestiones (Clarke 1910)
    50692: "Cicero_pg50692.txt",  # Cicero: Letters to Atticus, Vol. 2 of 3
    51403: "Cicero_pg51403.txt",  # Cicero: Letters to Atticus, Vol. 3 of 3
    54161: "The_republic_of_Cicero_Translated_from_the_Latin_and_Accompanied_With.txt",  # The republic of Cicero Translated from the Latin; and Accomp
    54717: "Two_Dramatizations_from_Vergil.txt",  # Two Dramatizations from Vergil: I. Dido—the Phœnecian Queen;
    58418: "Cicero_pg58418.txt",  # Cicero: Letters to Atticus, Vol. 1 of 3
    61596: "The_Aeneid_of_Virgil_pg61596.txt",  # The Aeneid of Virgil
    64024: "Translations_from_Lucretius.txt",  # Translations from Lucretius
    66399: "Virgil_Lucretius_Passages_translated_by_William_Stebbing.txt",  # Virgil & Lucretius Passages translated by William Stebbing
    73488: "The_Æneid_of_Virgil_translated_into_English_prose.txt",  # The Æneid of Virgil translated into English prose

    # --- LATE ANTIQUITY (Greek/Latin bridge) ---
    48664: "The_Works_of_the_Emperor_Julian_Vol_1.txt",  # The Works of the Emperor Julian, Vol. 1
    48768: "The_Works_of_the_Emperor_Julian_Vol_2.txt",  # The Works of the Emperor Julian, Vol. 2
    51443: "Claudian_volume_1_of_2_With_an_English_translation_by_Maurice_Platnaue.txt",  # Claudian, volume 1 (of 2) With an English translation by Mau
    51444: "Claudian_volume_2_of_2_With_an_English_translation_by_Maurice_Platnaue.txt",  # Claudian, volume 2 (of 2) With an English translation by Mau
    61614: "The_Apostolic_Tradition_of_Hippolytus_Translated_into_English_with_Int.txt",  # The Apostolic Tradition of Hippolytus Translated into Englis
    63300: "Iamblichus_Life_of_Pythagoras_or_Pythagoric_Life_Accompanied_by_Fragme.txt",  # Iamblichus' Life of Pythagoras, or Pythagoric Life Accompani
    65478: "Philosophumena_or_The_refutation_of_all_heresies_Volume_I.txt",  # Philosophumena; or, The refutation of all heresies, Volume I
    67116: "Philosophumena_or_The_refutation_of_all_heresies_Volume_II.txt",  # Philosophumena; or, The refutation of all heresies, Volume I
    70850: "Ptolemys_Tetrabiblos.txt",  # Ptolemy's Tetrabiblos
    71937: "The_writings_of_Clement_of_Alexandria_Vol_1_of_2.txt",  # The writings of Clement of Alexandria, Vol. 1 (of 2)
    72815: "Iamblichus_on_the_mysteries_of_the_Egyptians_Chaldeans_and_Assyrians.txt",  # Iamblichus on the mysteries of the Egyptians, Chaldeans, and
    73020: "The_writings_of_Clement_of_Alexandria_Vol_2_of_2.txt",  # The writings of Clement of Alexandria, Vol. 2 (of 2)
    74253: "The_philosophical_and_mathematical_commentaries_of_Proclus_on_the_firs.txt",  # The philosophical and mathematical commentaries of Proclus o
    77014: "Select_works_of_Porphyry.txt",  # Select works of Porphyry
    77393: "The_six_books_of_Proclus_the_Platonic_successor_on_the_theol_pg77393.txt",  # The six books of Proclus, the Platonic successor, on the the
    78800: "The_six_books_of_Proclus_the_Platonic_successor_on_the_theol_pg78800.txt",  # The six books of Proclus, the Platonic successor, on the the

    # --- ANCIENT NEAR EAST / HEBREW WORLD ---
    17150: "The_Oldest_Code_of_Laws_in_the_World_The_code_of_laws_promulgated_by_H.txt",  # The Oldest Code of Laws in the World The code of laws promul

    # --- ARABIC / PERSIAN (to 1300) ---
    246: "The_Rubaiyat_of_Omar_Khayyam.txt",  # The Rubaiyat of Omar Khayyam
    13086: "The_Diwan_of_Abul-Ala.txt",  # The Diwan of Abu'l-Ala
    16831: "The_Improvement_of_Human_Reason_Exhibited_in_the_Life_of_Hai_Ebn_Yokdh.txt",  # The Improvement of Human Reason Exhibited in the Life of Hai
    22535: "Rubáiyát_of_Omar_Khayyám_and_Salámán_and_Absál_Together_with_a_Life_of.txt",  # Rubáiyát of Omar Khayyám, and Salámán and Absál Together wit
    34572: "The_Awakening_of_the_Soul.txt",  # The Awakening of the Soul
    35260: "Rubáiyát_of_Omar_Khayyam_Rendered_into_English_Verse.txt",  # Rubáiyát of Omar Khayyam, Rendered into English Verse
    38511: "The_Sufistic_Quatrains_of_Omar_Khayyam.txt",  # The Sufistic Quatrains of Omar Khayyam
    45159: "The_Persian_Mystics.txt",  # The Persian Mystics: Jalálu'd-dín Rúmí
    50457: "The_Luzumiyat_of_Abul-Ala_Selected_from_his_Luzum_ma_la_Yalzam_and_Suc.txt",  # The Luzumiyat of Abu'l-Ala Selected from his Luzum ma la Yal
    50619: "The_Sufism_of_the_Rubáiyát_or_the_Secret_of_the_Great_Paradox.txt",  # The Sufism of the Rubáiyát, or, the Secret of the Great Para
    57068: "The_Festival_of_Spring_from_the_Díván_of_Jeláleddín_Rendered_in_Englis.txt",  # The Festival of Spring, from the Díván of Jeláleddín Rendere
    58186: "A_Compendium_on_the_Soul.txt",  # A Compendium on the Soul
    61724: "The_Mesnevi.txt",  # The Mesnevi

    # --- CHINESE (to 1300) ---
    2090: "Peach_Blossom_Shangri-la.txt",  # Peach Blossom Shangri-la: Tao Hua Yuan Ji
    2124: "A_Record_of_Buddhistic_Kingdoms_Being_an_account_by_the_Chin_pg2124.txt",  # A Record of Buddhistic Kingdoms Being an account by the Chin
    3330: "The_Analects_of_Confucius_from_the_Chinese_Classics.txt",  # The Analects of Confucius (from the Chinese Classics)
    24055: "The_Sayings_of_Confucius.txt",  # The Sayings of Confucius
    33815: "The_Wisdom_of_Confucius_with_Critical_and_Biographical_Sketches.txt",  # The Wisdom of Confucius with Critical and Biographical Sketc
    46389: "The_Sayings_of_Confucius_A_New_Translation_of_the_Greater_Part_of_the.txt",  # The Sayings of Confucius A New Translation of the Greater Pa
    64535: "A_Record_of_Buddhistic_Kingdoms_Being_an_account_by_the_Chin_pg64535.txt",  # A Record of Buddhistic Kingdoms Being an account by the Chin
    75878: "The_book_of_filial_duty.txt",  # The book of filial duty

    # --- OTHER ANCIENT ---
    7825: "Geometrical_Solutions_Derived_from_Mechanics_a_Treatise_of_Archimedes.txt",  # Geometrical Solutions Derived from Mechanics; a Treatise of 
    9610: "The_Elegies_of_Tibullus_Being_the_Consolations_of_a_Roman_Lover_Done_i.txt",  # The Elegies of Tibullus Being the Consolations of a Roman Lo
    12140: "Roman_Farm_Management.txt",  # Roman Farm Management: The Treatises of Cato and Varro
    58242: "The_Characters_of_Theophrastus_A_Translation_with_Introduction.txt",  # The Characters of Theophrastus A Translation, with Introduct

    # --- OTHER MEDIEVAL (author d. ≤1300) ---
    1949: "On_the_Ruin_of_Britain.txt",  # On the Ruin of Britain
    4370: "The_Deeds_of_God_Through_the_Franks.txt",  # The Deeds of God Through the Franks
    7015: "Buddhist_Psalms_translated_from_the_Japanese_of_Shinran_Shonin.txt",  # Buddhist Psalms translated from the Japanese of Shinran Shon
    14726: "The_Elder_Eddas_of_Saemund_Sigfusson_and_the_Younger_Eddas_of_Snorre_S.txt",  # The Elder Eddas of Saemund Sigfusson; and the Younger Eddas 
    18299: "The_Norwegian_account_of_Hacos_expedition_against_Scotland_AD_MCCLXIII.txt",  # The Norwegian account of Haco's expedition against Scotland,
    25761: "St_Bernard_of_Clairvauxs_Life_of_St_Malachy_of_Armagh.txt",  # St. Bernard of Clairvaux's Life of St. Malachy of Armagh
    35811: "Matelda_and_the_cloister_of_Hellfde.txt",  # Matelda and the cloister of Hellfde
    36402: "On_Union_with_God.txt",  # On Union with God
    38334: "The_Homilies_of_the_Anglo-Saxon_Church_Containing_the_Sermones_Catholi.txt",  # The Homilies of the Anglo-Saxon Church Containing the Sermon
    43319: "The_Symbolism_of_Churches_and_Church_Ornaments_A_Translation_of_the_Fi.txt",  # The Symbolism of Churches and Church Ornaments A Translation
    47297: "Parzival_pg47297.txt",  # Parzival: A Knightly Epic (vol. 1 of 2)
    47298: "Parzival_pg47298.txt",  # Parzival: A Knightly Epic (vol. 2 of 2)
    48870: "Early_Lives_of_Charlemagne_by_Eginhard_and_the_Monk_of_St_Gall_edited.txt",  # Early Lives of Charlemagne by Eginhard and the Monk of St Ga
    50040: "St_Benedicts_Rule_for_Monasteries.txt",  # St. Benedict's Rule for Monasteries
    50778: "William_of_Malmesburys_Chronicle_of_the_Kings_of_England_From_the_earl.txt",  # William of Malmesbury's Chronicle of the Kings of England Fr
    58393: "The_Mirror_of_Alchimy.txt",  # The Mirror of Alchimy
    58977: "The_Confessions_of_Al_Ghazzali.txt",  # The Confessions of Al Ghazzali
    59770: "The_Plays_of_Roswitha.txt",  # The Plays of Roswitha
    65708: "The_Philosophy_and_Theology_of_Averroes.txt",  # The Philosophy and Theology of Averroes
    66025: "The_Lady_Poverty.txt",  # The Lady Poverty: A XIII. Century Allegory
    70531: "The_seven_books_of_Paulus_Ægineta_volume_1_of_3.txt",  # The seven books of Paulus Ægineta, volume 1 (of 3)
    70532: "The_seven_books_of_Paulus_Ægineta_volume_2_of_3.txt",  # The seven books of Paulus Ægineta, volume 2 (of 3)
    70533: "The_seven_books_of_Paulus_Ægineta_volume_3_of_3.txt",  # The seven books of Paulus Ægineta, volume 3 (of 3)
    71142: "A_translation_of_Glanville.txt",  # A translation of Glanville
    73140: "Some_religious_and_moral_teachings_of_Al-Ghazzali.txt",  # Some religious and moral teachings of Al-Ghazzali
    73584: "The_guide_for_the_perplexed.txt",  # The guide for the perplexed
    76016: "The_pillow-book_of_Sei_Shōnagon.txt",  # The pillow-book of Sei Shōnagon
    79139: "The_library_of_Photius_Volume_1_of_1.txt",  # The library of Photius, Volume 1 (of 1)

    # --- COMPILATIONS, DICTIONARIES & MODERN SCHOLARSHIP ---
    10625: "Concise_Dictionary_of_Middle_English_1150_1580.txt",  # A Concise Dictionary of Middle English from A.D. 1150 to 158
    14019: "Harvard_Classics_Vol49_Epic_and_Saga.txt",  # The Harvard Classics, Volume 49, Epic and Saga With Introduc
    37342: "Medieval_English_Literature_Ker.txt",  # Medieval English Literature
    43555: "Selections_from_Early_Middle_English_1130_1250_Part2_Notes.txt",  # Selections from Early Middle English, 1130-1250. Part 2: Not
}

# Post-1300 / borderline PG texts often confused with this corpus (DO NOT merge):
EXCLUDED_POST_1300: dict[int, str] = {
    14568: "Sir Gawayne and the Green Knight — MS ~1360; EXCLUDE (post-1300)",
    66084: "Sir Gawain retelling Weston — based on post-1300 poem; EXCLUDE",
    2383: "Canterbury Tales Chaucer — late 14th c.; EXCLUDE",
    1251: "Le Morte d'Arthur Malory — 15th c.; EXCLUDE",
    1252: "Le Morte d'Arthur vol 2; EXCLUDE",
}

# Non-Gutenberg public-domain English, shipped via URL sources in mvp_works.py:
#   Aratus, Phaenomena — Mair 1921, Theoi (Loeb)
#   Aristotle, On the Heavens — Stocks 1922, MIT Internet Classics Archive
#   Surya Siddhanta — Burgess 1860, Internet Archive EPUB
#   Ptolemy's Catalogue of Stars — Peters & Knobel 1915, Internet Archive PDF
# Schjellerup's French Book of Fixed Stars is deferred until non-English volumes
# have a language story.
